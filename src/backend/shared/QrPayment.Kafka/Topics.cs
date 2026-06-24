// QrPayment.Kafka / Topics.cs
//
// Tüm Kafka topic isimlerinin merkezi kataloğu.
// Servisler bu sabitleri kullanarak topic isimlerini hardcode etmez;
// böylece yeniden adlandırma tek noktadan yapılabilir.
//
// Topic adlandırma kuralı: "domain.olay_fiili" (küçük harf, nokta ayraç)
// Partition ve retention bilgileri IMPLEMENTATION-PLAN-EXTENDED.md'dedir.

namespace QrPayment.Kafka;

public static class Topics
{
    // ─── Onboarding domain ───────────────────────────────────────────────────
    /// <summary>Müşteri kayıt tamamlandı → WalletService dinler</summary>
    public const string CustomerRegistered = "customer.registered";

    /// <summary>KYC onaylandı → WalletService cüzdanı aktif eder</summary>
    public const string CustomerKycApproved = "customer.kyc_approved";

    /// <summary>KYC reddedildi → gelecekte NotificationService dinleyebilir</summary>
    public const string CustomerKycRejected = "customer.kyc_rejected";

    /// <summary>İşyeri onaylandı → AuthService merchant hesabı açar</summary>
    public const string MerchantApproved = "merchant.approved";

    /// <summary>Terminal oluşturuldu → AuthService HMAC credential kaydeder</summary>
    public const string TerminalCreated = "terminal.created";

    // ─── Payment domain ───────────────────────────────────────────────────────
    /// <summary>Ödeme başarılı → ReportingService ES yazar + PDF üretir + FCM push</summary>
    public const string PaymentCompleted = "payment.completed";

    /// <summary>Ödeme başarısız → ReportingService ES'e yazar</summary>
    public const string PaymentFailed = "payment.failed";

    /// <summary>Ödeme iptal/reversal → ReportingService reversal kaydı yazar</summary>
    public const string PaymentReversed = "payment.reversed";
}
