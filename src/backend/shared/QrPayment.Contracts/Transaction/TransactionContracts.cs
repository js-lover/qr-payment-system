// QrPayment.Contracts / Transaction / TransactionContracts.cs
//
// TransactionService HTTP endpoint'lerinin DTO'ları.
// Ödeme onayı akışı asenkrondur:
//   1. POST /v1/payments/confirm → 202 Accepted (işlem başladı)
//   2. SignalR üzerinden sonuç gelir (success/failed/reversed)
//   3. GET /v1/payments/{id} ile de durum sorgulanabilir.
//
// Asenkron tasarım tercihinin nedeni: ISO 8583 TCP iletişimi 200-500ms sürer,
// bu süre boyunca HTTP bağlantısı açık tutulmamalıdır.

namespace QrPayment.Contracts.Transaction;

/// <summary>
/// Müşteri ödeme onay isteği.
/// Mobil uygulama QR'ı okuyup kullanıcı "Onayla" dediğinde gönderilir.
/// WalletId JWT token'dan çıkarılır (istek body'sinde gönderilmez).
/// </summary>
public record PaymentConfirmRequest
{
    /// <summary>Müşterinin kamerasıyla okuduğu QR içeriğinden parse edilen token.</summary>
    public Guid QrToken { get; init; }

    /// <summary>Cihaz parmak izi — fraud tespiti için (opsiyonel).</summary>
    public string? DeviceFingerprint { get; init; }
}

/// <summary>
/// POST /v1/payments/confirm yanıtı.
/// 202 Accepted döner; gerçek sonuç SignalR ile iletilir.
/// </summary>
public record PaymentConfirmResponse
{
    public Guid TransactionId { get; init; }

    /// <summary>Anlık durum: genellikle "PROCESSING"</summary>
    public string Status { get; init; } = "PROCESSING";

    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";

    /// <summary>İstemcinin SignalR'a bağlanacağı hub path'i.</summary>
    public string SignalREndpoint { get; init; } = "/hubs/payment";

    /// <summary>İstemcinin katılacağı SignalR grubu: "transaction:{transactionId}"</summary>
    public string SignalRGroup { get; init; } = string.Empty;
}

/// <summary>
/// GET /v1/payments/{id} yanıtı — polling tercih eden istemciler için.
/// Normal akışta SignalR kullanılır, bu endpoint fallback görevi görür.
/// </summary>
public record PaymentStatusResponse
{
    public Guid TransactionId { get; init; }

    /// <summary>İşlem durumu: "PENDING" | "PROCESSING" | "SUCCESS" | "FAILED" | "REVERSED"</summary>
    public string Status { get; init; } = string.Empty;

    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public string MerchantTitle { get; init; } = string.Empty;

    /// <summary>ISO 8583 Field 39 yanıt kodu. "00"=başarılı, "51"=yetersiz bakiye, "91"=banka hatası.</summary>
    public string? IsoRespCode { get; init; }

    public string? Stan { get; init; }
    public string? Rrn { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Başarılı işlemlerde PDF makbuz URL'si.</summary>
    public string? ReceiptUrl { get; init; }
}
