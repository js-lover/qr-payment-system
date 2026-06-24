// QrPayment.Kafka / Events / BaseEvent.cs
//
// Kafka'ya publish edilen tüm event'lerin ortak zarfı.
// Her event şunları taşır:
//   - EventId    : idempotency kontrolü için benzersiz UUID
//   - EventType  : consumer'ın hangi event handler'ı çalıştıracağını belirler
//   - Timestamp  : event'in oluşturulma anı (UTC)
//   - Version    : schema değişikliklerinde geriye dönük uyumluluk için
//
// Somut event'ler bu record'dan türetilir ve Payload alanına
// domain'e özgü veri taşır.

namespace QrPayment.Kafka.Events;

/// <summary>
/// Tüm Kafka event'lerinin tabanı. Confluent.Kafka JSON serializer
/// bu tip hiyerarşisini otomatik çözer.
/// </summary>
public abstract record BaseEvent
{
    /// <summary>Aynı event'in birden fazla kez işlenmesini önlemek için benzersiz kimlik.</summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Topic routing ve handler seçimi için kullanılır. Örn: "payment.completed"</summary>
    public abstract string EventType { get; }

    /// <summary>Event'in oluşturulduğu UTC zamanı.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Schema versiyonu. Geriye dönük uyumluluk için.</summary>
    public string Version { get; init; } = "1.0";
}

// ─── Onboarding Events ───────────────────────────────────────────────────────

/// <summary>
/// Müşteri SMS OTP doğrulamasını geçip kayıt olduğunda OnboardingService tarafından yayınlanır.
/// WalletService bu event'i dinleyerek pasif cüzdan oluşturur.
/// </summary>
public record CustomerRegisteredEvent : BaseEvent
{
    public override string EventType => "customer.registered";
    public Guid CustomerId { get; init; }
    public string Gsm { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string KycStatus { get; init; } = "PENDING";
}

/// <summary>
/// Admin KYC başvurusunu onayladığında yayınlanır.
/// WalletService cüzdanı "ACTIVE" durumuna getirir.
/// </summary>
public record CustomerKycApprovedEvent : BaseEvent
{
    public override string EventType => "customer.kyc_approved";
    public Guid CustomerId { get; init; }
    public Guid ApprovedBy { get; init; }
    public DateTimeOffset ApprovedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Admin KYC başvurusunu reddettiğinde yayınlanır.
/// Gelecekte NotificationService bu event'i kullanabilir.
/// </summary>
public record CustomerKycRejectedEvent : BaseEvent
{
    public override string EventType => "customer.kyc_rejected";
    public Guid CustomerId { get; init; }
    public string RejectionReason { get; init; } = string.Empty;
}

/// <summary>
/// İşyeri başvurusu onaylandığında yayınlanır.
/// AuthService bu event'i dinleyerek merchant kullanıcı hesabı açar.
/// </summary>
public record MerchantApprovedEvent : BaseEvent
{
    public override string EventType => "merchant.approved";
    public Guid MerchantId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TaxNumber { get; init; } = string.Empty;
    public Guid ApprovedBy { get; init; }
}

/// <summary>
/// Yeni terminal oluşturulduğunda yayınlanır.
/// AuthService terminal credential kaydını oluşturur.
/// </summary>
public record TerminalCreatedEvent : BaseEvent
{
    public override string EventType => "terminal.created";
    public string TerminalId { get; init; } = string.Empty;
    public Guid MerchantId { get; init; }
    public Guid BranchId { get; init; }

    /// <summary>HMAC-SHA256 imzalama için paylaşılan gizli anahtar (Base64).</summary>
    public string SecretKey { get; init; } = string.Empty;
}

// ─── Payment Events ───────────────────────────────────────────────────────────

/// <summary>
/// Ödeme başarıyla tamamlandığında TransactionService tarafından yayınlanır.
/// ReportingService: Elasticsearch'e yazar, PDF makbuz üretir, FCM push gönderir.
/// </summary>
public record PaymentCompletedEvent : BaseEvent
{
    public override string EventType => "payment.completed";
    public Guid TransactionId { get; init; }
    public Guid WalletId { get; init; }
    public Guid MerchantId { get; init; }
    public string MerchantTitle { get; init; } = string.Empty;
    public string TerminalId { get; init; } = string.Empty;
    public Guid QrToken { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";

    /// <summary>ISO 8583 Field 39 yanıt kodu. "00" = başarılı.</summary>
    public string IsoRespCode { get; init; } = string.Empty;

    /// <summary>System Trace Audit Number — banka tarafında eşleştirme için.</summary>
    public string Stan { get; init; } = string.Empty;

    /// <summary>Retrieval Reference Number — banka tarafında eşleştirme için.</summary>
    public string Rrn { get; init; } = string.Empty;

    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Firebase Cloud Messaging token — push notification göndermek için.</summary>
    public string? FcmToken { get; init; }
}

/// <summary>
/// Ödeme başarısız olduğunda (yetersiz bakiye, banka hatası vb.) yayınlanır.
/// ReportingService başarısız işlemi Elasticsearch'e yazar.
/// </summary>
public record PaymentFailedEvent : BaseEvent
{
    public override string EventType => "payment.failed";
    public Guid TransactionId { get; init; }
    public Guid WalletId { get; init; }
    public Guid MerchantId { get; init; }
    public string TerminalId { get; init; } = string.Empty;
    public Guid QrToken { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string IsoRespCode { get; init; } = string.Empty;

    /// <summary>İnsan tarafından okunabilen başarısızlık nedeni. Örn: "INSUFFICIENT_FUNDS"</summary>
    public string FailReason { get; init; } = string.Empty;

    public DateTimeOffset FailedAt { get; init; }
}

/// <summary>
/// Ödeme timeout veya banka hatası nedeniyle iptal edildiğinde yayınlanır.
/// ReportingService reversal kaydını Elasticsearch'e yazar.
/// </summary>
public record PaymentReversedEvent : BaseEvent
{
    public override string EventType => "payment.reversed";
    public Guid TransactionId { get; init; }

    /// <summary>Orijinal işlemin STAN değeri — 0420 reversal mesajıyla eşleştirilir.</summary>
    public string OriginalStan { get; init; } = string.Empty;

    /// <summary>İptal nedeni: "TIMEOUT" | "BANK_ERROR" | "MANUAL"</summary>
    public string ReversalReason { get; init; } = string.Empty;

    public DateTimeOffset ReversedAt { get; init; }
}
