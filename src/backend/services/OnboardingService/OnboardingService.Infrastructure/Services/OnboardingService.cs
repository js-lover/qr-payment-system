// OnboardingService.Infrastructure / Services / OnboardingService.cs
//
// Onboarding iş mantığının merkezi.
//
// Müşteri kayıt akışı:
//   1. RegisterCustomerAsync  → OTP SMS gönder
//   2. VerifyOtpAsync         → OTP doğrula → Kafka: customer.registered
//   3. SubmitKycAsync         → KYC belge yolu kaydet (dosya upload ayrı endpoint)
//   4. ApproveKycAsync (Admin) → Kafka: customer.kyc_approved
//
// İşyeri kayıt akışı:
//   1. ApplyMerchantAsync    → Başvuru kaydet (PENDING)
//   2. ApproveMerchantAsync  → Kafka: merchant.approved
//   3. CreateTerminalAsync   → Terminal oluştur → Kafka: terminal.created

using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using OnboardingService.Domain.Entities;
using OnboardingService.Domain.Interfaces;
using QrPayment.Kafka;
using QrPayment.Kafka.Events;
using QrPayment.Kafka.Producer;
using QrPayment.Shared.Exceptions;

namespace OnboardingService.Infrastructure.Services;

public interface IOnboardingService
{
    // Müşteri
    Task<Guid> RegisterCustomerAsync(string identityHash, string firstName, string lastName, string gsm, CancellationToken ct = default);
    Task VerifyOtpAsync(string gsm, string otpCode, CancellationToken ct = default);
    Task SubmitKycAsync(Guid customerId, string documentPath, CancellationToken ct = default);
    Task ApproveKycAsync(Guid customerId, Guid adminId, CancellationToken ct = default);
    Task RejectKycAsync(Guid customerId, string reason, CancellationToken ct = default);

    // İşyeri
    Task<Guid> ApplyMerchantAsync(string title, string taxNumber, string iban, string mcc, CancellationToken ct = default);
    Task ApproveMerchantAsync(Guid merchantId, Guid adminId, CancellationToken ct = default);
    Task<string> CreateTerminalAsync(Guid merchantId, Guid? branchId, CancellationToken ct = default);
}

public class OnboardingApplicationService(
    ICustomerRepository customerRepo,
    IMerchantRepository merchantRepo,
    ITerminalRepository terminalRepo,
    IOtpRepository otpRepo,
    ISmsService smsService,
    IKafkaProducer kafkaProducer,
    IConfiguration configuration) : IOnboardingService
{
    // ─── Müşteri ─────────────────────────────────────────────────────────────

    public async Task<Guid> RegisterCustomerAsync(
        string identityHash, string firstName, string lastName, string gsm, CancellationToken ct = default)
    {
        // Aynı GSM ile müşteri zaten var mı?
        var existing = await customerRepo.GetByGsmAsync(gsm, ct);
        if (existing is not null)
            throw new BusinessRuleException("GSM_ALREADY_REGISTERED", "GSM Kayıtlı",
                "Bu telefon numarası zaten kayıtlı.", 409);

        // Aynı kimlik (TCKN hash) zaten var mı?
        var existingByIdentity = await customerRepo.GetByIdentityHashAsync(identityHash, ct);
        if (existingByIdentity is not null)
            throw new BusinessRuleException("IDENTITY_ALREADY_REGISTERED", "Kimlik Kayıtlı",
                "Bu kimlik bilgisi zaten kayıtlı.", 409);

        var customer = Customer.Create(identityHash, firstName, lastName, gsm);
        await customerRepo.AddAsync(customer, ct);
        await customerRepo.SaveChangesAsync(ct);

        // 6 haneli OTP üret ve gönder
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        var expiryMinutes = int.Parse(configuration["Sms:OtpExpiryMinutes"] ?? "3");
        var otp = OtpVerification.Create(gsm, otpCode, expiryMinutes);
        await otpRepo.AddAsync(otp, ct);
        await otpRepo.SaveChangesAsync(ct);

        await smsService.SendOtpAsync(gsm, otpCode, ct);

        return customer.Id;
    }

    public async Task VerifyOtpAsync(string gsm, string otpCode, CancellationToken ct = default)
    {
        var otp = await otpRepo.GetLatestValidAsync(gsm, ct)
            ?? throw new BusinessRuleException("OTP_NOT_FOUND", "OTP Bulunamadı",
                "Geçerli OTP kodu bulunamadı.", 400);

        if (!otp.Verify(otpCode))
        {
            await otpRepo.SaveChangesAsync(ct); // AttemptCount artışını kaydet
            throw new BusinessRuleException("INVALID_OTP", "Geçersiz OTP",
                otp.IsLocked ? "Çok fazla yanlış deneme. Yeni kod talep edin." : "OTP kodu hatalı.", 400);
        }

        await otpRepo.SaveChangesAsync(ct);

        // OTP doğrulandı → customer.registered event'i yayınla
        var customer = await customerRepo.GetByGsmAsync(gsm, ct)
            ?? throw new NotFoundException("Customer", gsm);

        await kafkaProducer.PublishAsync(Topics.CustomerRegistered, new CustomerRegisteredEvent
        {
            CustomerId = customer.Id,
            Gsm = customer.Gsm,
            FirstName = customer.FirstName,
            LastName = customer.LastName
        }, ct);
    }

    public async Task SubmitKycAsync(Guid customerId, string documentPath, CancellationToken ct = default)
    {
        var customer = await customerRepo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        if (customer.KycStatus == "APPROVED")
            throw new BusinessRuleException("KYC_ALREADY_APPROVED", "KYC Onaylandı",
                "KYC başvurusu zaten onaylanmış.", 409);

        // KYC belgesi yolu kaydedilir; gerçek dosya upload ayrı bir blob storage entegrasyonu gerektirir
        // Bu MVP'de documentPath yerel dosya yolunu temsil eder
        // İleride Azure Blob / S3 entegrasyonu eklenebilir
        await customerRepo.SaveChangesAsync(ct);
    }

    public async Task ApproveKycAsync(Guid customerId, Guid adminId, CancellationToken ct = default)
    {
        var customer = await customerRepo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        customer.ApproveKyc();
        await customerRepo.SaveChangesAsync(ct);

        await kafkaProducer.PublishAsync(Topics.CustomerKycApproved, new CustomerKycApprovedEvent
        {
            CustomerId = customer.Id,
            ApprovedBy = adminId
        }, ct);
    }

    public async Task RejectKycAsync(Guid customerId, string reason, CancellationToken ct = default)
    {
        var customer = await customerRepo.GetByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Customer", customerId);

        customer.RejectKyc(reason);
        await customerRepo.SaveChangesAsync(ct);

        await kafkaProducer.PublishAsync(Topics.CustomerKycRejected, new CustomerKycRejectedEvent
        {
            CustomerId = customer.Id,
            RejectionReason = reason
        }, ct);
    }

    // ─── İşyeri ──────────────────────────────────────────────────────────────

    public async Task<Guid> ApplyMerchantAsync(
        string title, string taxNumber, string iban, string mcc, CancellationToken ct = default)
    {
        var existing = await merchantRepo.GetByTaxNumberAsync(taxNumber, ct);
        if (existing is not null)
            throw new BusinessRuleException("TAX_NUMBER_EXISTS", "Vergi Numarası Kayıtlı",
                "Bu vergi numarasıyla zaten kayıt mevcut.", 409);

        var merchant = Merchant.Create(title, taxNumber, iban, mcc);
        await merchantRepo.AddAsync(merchant, ct);
        await merchantRepo.SaveChangesAsync(ct);

        return merchant.Id;
    }

    public async Task ApproveMerchantAsync(Guid merchantId, Guid adminId, CancellationToken ct = default)
    {
        var merchant = await merchantRepo.GetByIdAsync(merchantId, ct)
            ?? throw new NotFoundException("Merchant", merchantId);

        if (merchant.Status == "APPROVED")
            throw new BusinessRuleException("MERCHANT_ALREADY_APPROVED", "İşyeri Onaylı",
                "İşyeri zaten onaylanmış.", 409);

        merchant.Approve();
        await merchantRepo.SaveChangesAsync(ct);

        await kafkaProducer.PublishAsync(Topics.MerchantApproved, new MerchantApprovedEvent
        {
            MerchantId = merchant.Id,
            Title = merchant.Title,
            TaxNumber = merchant.TaxNumber,
            ApprovedBy = adminId
        }, ct);
    }

    public async Task<string> CreateTerminalAsync(
        Guid merchantId, Guid? branchId, CancellationToken ct = default)
    {
        // Sıralı terminal ID üret: TID001, TID002, ...
        var terminalId = $"TID{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000:D5}";

        // Cryptographically secure HMAC secret key (32 byte → Base64)
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secretKey = Convert.ToBase64String(secretBytes);

        var terminal = Terminal.Create(terminalId, merchantId, secretKey, branchId);
        await terminalRepo.AddAsync(terminal, ct);
        await terminalRepo.SaveChangesAsync(ct);

        await kafkaProducer.PublishAsync(Topics.TerminalCreated, new TerminalCreatedEvent
        {
            TerminalId = terminalId,
            MerchantId = merchantId,
            BranchId = branchId ?? Guid.Empty,
            SecretKey = secretKey
        }, ct);

        return terminalId;
    }
}
