// TransactionService.Domain / Entities / Transaction.cs
//
// Ödeme işlemi entity'si.
//
// Durum makinesi:
//   PROCESSING → (COMPLETED | FAILED | REVERSED)
//
// ISO 8583 entegrasyonu:
//   Bank Simulator'a TCP üzerinden ISO 8583 mesajı gönderilir.
//   IsoResponseCode "00" → başarılı, diğer kodlar → başarısız.
//   STAN (System Trace Audit Number): 6 haneli sıra numarası, her işlemde artar.
//   RRN (Retrieval Reference Number): 12 haneli tarih+rastgele, banka tarafı referansı.
//
// SignalR:
//   POS terminali ödeme sonucunu beklerken SignalR üzerinden anlık bildirim alır.
//   GroupName: "payment:{transactionId}" formatında.

namespace TransactionService.Domain.Entities;

public class Transaction
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Claim edilen QR token UUID'si.</summary>
    public string QrToken { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }
    public Guid MerchantId { get; private set; }
    public Guid TerminalId { get; private set; }

    /// <summary>İşlem tutarı (kuruş cinsinden, long).</summary>
    public long AmountKurus { get; private set; }

    public string Currency { get; private set; } = "TRY";

    /// <summary>İşlem durumu: PROCESSING | COMPLETED | FAILED | REVERSED</summary>
    public string Status { get; private set; } = "PROCESSING";

    /// <summary>ISO 8583 yanıt kodu. "00" = başarılı.</summary>
    public string? IsoResponseCode { get; private set; }

    /// <summary>System Trace Audit Number — ISO 8583 saha 11.</summary>
    public string? Stan { get; private set; }

    /// <summary>Retrieval Reference Number — ISO 8583 saha 37.</summary>
    public string? Rrn { get; private set; }

    /// <summary>İşlem tamamlanma zamanı.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Transaction() { }

    public static Transaction Create(
        string qrToken, Guid customerId, Guid merchantId, Guid terminalId, long amountKurus)
        => new()
        {
            QrToken = qrToken,
            CustomerId = customerId,
            MerchantId = merchantId,
            TerminalId = terminalId,
            AmountKurus = amountKurus
        };

    /// <summary>ISO 8583 "00" geldiğinde çağrılır. COMPLETED durumuna geçer.</summary>
    public void Complete(string isoResponseCode, string stan, string rrn)
    {
        IsoResponseCode = isoResponseCode;
        Stan = stan;
        Rrn = rrn;
        Status = "COMPLETED";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>ISO 8583 başarısız yanıt geldiğinde çağrılır. FAILED durumuna geçer.</summary>
    public void Fail(string isoResponseCode)
    {
        IsoResponseCode = isoResponseCode;
        Status = "FAILED";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Onaylanan işlem iade edildiğinde REVERSED durumuna geçer.</summary>
    public void Reverse()
    {
        if (Status != "COMPLETED")
            throw new InvalidOperationException("Yalnızca tamamlanmış işlemler iade edilebilir.");
        Status = "REVERSED";
    }
}
