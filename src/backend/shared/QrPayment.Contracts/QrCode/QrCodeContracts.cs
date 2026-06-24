// QrPayment.Contracts / QrCode / QrCodeContracts.cs
//
// QrCodeService HTTP endpoint'lerinin DTO'ları.
// QR token'ın içinde finansal bilgi BULUNMAZ — sadece UUID taşır.
// Finansal bilgi Redis'te token ile ilişkili olarak saklanır.
//
// Akış:
//   Terminal → POST /v1/qr/generate → { token: uuid, qrContent: "QRPAY:uuid" }
//   Terminal QR'ı ekranda gösterir.
//   Müşteri okur → GET /v1/qr/validate?token=uuid → tutar + işyeri bilgisi
//   Müşteri onaylar → TransactionService devralır.

namespace QrPayment.Contracts.QrCode;

/// <summary>
/// Terminal tarafından QR üretme isteği.
/// Kong API Gateway HMAC middleware'i bu isteği doğrular.
/// </summary>
public record GenerateQrRequest
{
    public string TerminalId { get; init; } = string.Empty;
    public Guid MerchantId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
}

/// <summary>QR üretme yanıtı. Terminal bu bilgilerle QR kodunu oluşturur ve ekranda gösterir.</summary>
public record GenerateQrResponse
{
    /// <summary>UUID token — QR içindeki veri bu token'dır.</summary>
    public Guid Token { get; init; }

    /// <summary>QR koda encode edilecek string. Format: "QRPAY:{uuid}"</summary>
    public string QrContent { get; init; } = string.Empty;

    /// <summary>Token'ın Redis'te ne zaman sileceği (şu an + 90 sn).</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>İstemcinin countdown timer'ı için.</summary>
    public int TtlSeconds { get; init; } = 90;

    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string MerchantTitle { get; init; } = string.Empty;
}

/// <summary>
/// Müşteri QR'ı okuduktan sonra /v1/qr/validate çağrısının yanıtı.
/// İstemci bu bilgileri onay ekranında kullanıcıya gösterir.
/// </summary>
public record ValidateQrResponse
{
    public Guid Token { get; init; }
    public Guid MerchantId { get; init; }
    public string MerchantTitle { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";

    /// <summary>Token durumu: "ACTIVE" | "PROCESSING" | "USED" | "EXPIRED"</summary>
    public string Status { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Müşterinin göreceği kalan süre (saniye).</summary>
    public int RemainingSeconds { get; init; }
}
