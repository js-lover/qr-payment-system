// TransactionService.Infrastructure / Services / PaymentService.cs
//
// Ödeme orkestrasyon servisi.
//
// Ödeme akışı:
//   1. Müşteri QR token'ı doğrula (QrCodeService'e HTTP çağrı)
//   2. QR token'ı claim et (SETNX — race condition koruması)
//   3. Transaction kaydı oluştur (PROCESSING)
//   4. WalletService'e provision isteği gönder (bakiyeyi bloke et)
//   5. ISO 8583 Bank Simulator'a authorization request gönder
//   6a. "00" → WalletService.ConfirmProvision → Transaction.Complete → Kafka: payment.completed
//   6b. Diğer → WalletService.ReleaseProvision → Transaction.Fail → Kafka: payment.failed
//   7. SignalR ile POS terminali anlık bildirim
//
// Not: Bu MVP'de QrCodeService ve WalletService'e HttpClient üzerinden çağrı yapılır.
// Gerçek üretimde servis discovery (Consul/k8s) veya dahili gRPC tercih edilebilir.

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using TransactionService.Domain.Entities;
using TransactionService.Domain.Interfaces;
using TransactionService.Infrastructure.Hubs;
using QrPayment.Kafka.Events;
using QrPayment.Kafka.Producer;
using QrPayment.Shared.Exceptions;

namespace TransactionService.Infrastructure.Services;

public interface IPaymentService
{
    Task<Transaction> InitiatePaymentAsync(string qrToken, Guid customerId, CancellationToken ct = default);
    Task<Transaction> GetStatusAsync(Guid transactionId, CancellationToken ct = default);
}

public class PaymentService(
    ITransactionRepository transactionRepo,
    IBankSimulatorClient bankClient,
    IKafkaProducer kafkaProducer,
    IHubContext<PaymentHub> paymentHub,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<Transaction> InitiatePaymentAsync(
        string qrToken, Guid customerId, CancellationToken ct = default)
    {
        // 1. QR token bilgilerini al (QrCodeService)
        var qrClient = httpClientFactory.CreateClient("QrCodeService");
        var qrResponse = await qrClient.GetAsync($"/qr/{qrToken}/validate", ct);

        if (!qrResponse.IsSuccessStatusCode)
            throw new BusinessRuleException("QR_INVALID", "Geçersiz QR",
                "QR kodu geçersiz veya süresi dolmuş.", 400);

        // 2. QR token'ı claim et (sadece bir müşteri claim edebilir)
        var claimClient = httpClientFactory.CreateClient("QrCodeService");
        var claimResponse = await claimClient.PostAsync($"/qr/{qrToken}/claim", null, ct);
        if (!claimResponse.IsSuccessStatusCode)
            throw new BusinessRuleException("QR_ALREADY_CLAIMED", "QR Zaten Kullanıldı",
                "Bu QR kodu zaten başka bir ödeme için kullanıldı.", 409);

        // 3. Transaction kaydı oluştur
        // QR bilgilerini parse et
        dynamic? qrData = await qrResponse.Content.ReadFromJsonAsync<dynamic>(ct);
        var merchantId = Guid.Parse((string)qrData!.data.merchantId);
        var terminalId = Guid.Parse((string)qrData.data.terminalId);
        var amountDecimal = (decimal)(double)qrData.data.amount;
        var amountKurus = (long)(amountDecimal * 100);

        var transaction = Transaction.Create(qrToken, customerId, merchantId, terminalId, amountKurus);
        await transactionRepo.AddAsync(transaction, ct);
        await transactionRepo.SaveChangesAsync(ct);

        // 4. Wallet provision (bakiye bloke)
        var walletClient = httpClientFactory.CreateClient("WalletService");
        var provisionResp = await walletClient.PostAsJsonAsync("/wallet/provision",
            new { amount = amountDecimal, qrToken }, ct);

        if (!provisionResp.IsSuccessStatusCode)
        {
            transaction.Fail("51"); // Yetersiz bakiye
            await transactionRepo.SaveChangesAsync(ct);
            await NotifyTerminalAsync(transaction, ct);
            return transaction;
        }

        // 5. ISO 8583 Bank Simulator
        var stan = GenerateStan();
        var rrn = GenerateRrn();
        var bankReq = new BankRequest(
            AmountKurus: amountKurus,
            TerminalId: terminalId.ToString("N")[..8].ToUpper(),
            MerchantId: merchantId.ToString("N")[..8].ToUpper(),
            Stan: stan,
            Rrn: rrn,
            MaskedPan: $"****{customerId:N}"[^4..]);

        var bankResp = await bankClient.AuthorizeAsync(bankReq, ct);

        if (bankResp.IsApproved)
        {
            // 6a. Başarılı: blokajı confirm et
            transaction.Complete(bankResp.ResponseCode, bankResp.Stan, bankResp.Rrn);
            await walletClient.PostAsJsonAsync("/wallet/confirm",
                new { amount = amountDecimal, transactionId = transaction.Id.ToString() }, ct);

            await kafkaProducer.PublishAsync(QrPayment.Kafka.Topics.PaymentCompleted,
                new PaymentCompletedEvent
                {
                    TransactionId = transaction.Id,
                    MerchantId = merchantId,
                    Amount = amountDecimal,
                    Stan = stan,
                    Rrn = rrn,
                    IsoRespCode = bankResp.ResponseCode,
                    CompletedAt = DateTimeOffset.UtcNow
                }, ct);
        }
        else
        {
            // 6b. Başarısız: blokajı serbest bırak
            transaction.Fail(bankResp.ResponseCode);
            await walletClient.PostAsJsonAsync("/wallet/release",
                new { amount = amountDecimal, qrToken }, ct);

            await kafkaProducer.PublishAsync(QrPayment.Kafka.Topics.PaymentFailed,
                new PaymentFailedEvent
                {
                    TransactionId = transaction.Id,
                    MerchantId = merchantId,
                    Amount = amountDecimal,
                    IsoRespCode = bankResp.ResponseCode,
                    FailReason = bankResp.ResponseCode == "51" ? "INSUFFICIENT_FUNDS" : "BANK_DECLINED",
                    FailedAt = DateTimeOffset.UtcNow
                }, ct);
        }

        await transactionRepo.SaveChangesAsync(ct);

        // 7. SignalR anlık bildirim
        await NotifyTerminalAsync(transaction, ct);

        return transaction;
    }

    public async Task<Transaction> GetStatusAsync(Guid transactionId, CancellationToken ct = default)
        => await transactionRepo.GetByIdAsync(transactionId, ct)
           ?? throw new NotFoundException("Transaction", transactionId);

    private async Task NotifyTerminalAsync(Transaction transaction, CancellationToken ct)
    {
        await paymentHub.Clients
            .Group($"payment:{transaction.Id}")
            .SendAsync("PaymentResult", new
            {
                transactionId = transaction.Id,
                status = transaction.Status,
                responseCode = transaction.IsoResponseCode
            }, ct);
    }

    // STAN: 6 haneli, her çağrıda artan (üretim için DB sequence kullanılmalı)
    private static string GenerateStan()
        => (DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1000000).ToString("D6");

    // RRN: 12 haneli tarih + rastgele
    private static string GenerateRrn()
        => $"{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
}
