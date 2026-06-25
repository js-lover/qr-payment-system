// QrPayment.Contracts / Transaction / TransactionContracts.cs
//
// TransactionService HTTP endpoint'lerinin DTO'ları.
//
// Ödeme onayı akışı:
//   1. POST /payments/confirm → 200 OK (işlem başladı, SignalR group döner)
//   2. SignalR "payment:{transactionId}" grubundan "PaymentResult" eventi bekle
//   3. GET  /payments/{id} ile de durum sorgulanabilir (polling fallback)

namespace QrPayment.Contracts.Transaction;

/// <summary>
/// Müşteri ödeme onay isteği.
/// QR token mobil uygulamada kamera ile okunur ve buraya gönderilir.
/// </summary>
public record PaymentConfirmRequest(
    string QrToken,
    string? DeviceFingerprint = null);

/// <summary>
/// POST /payments/confirm yanıtı.
/// Terminal ve müşteri SignalREndpoint'e bağlanarak sonucu bekler.
/// </summary>
public record PaymentConfirmResponse(
    Guid TransactionId,
    string Status,
    string SignalREndpoint,
    string SignalRGroup);

/// <summary>
/// GET /payments/{id} yanıtı.
/// ISO 8583 sonuçlarını ve tamamlanma bilgisini içerir.
/// </summary>
public record PaymentStatusResponse(
    Guid TransactionId,
    string Status,
    string? IsoResponseCode,
    string? Stan,
    string? Rrn,
    DateTimeOffset? CompletedAt,
    string? ReceiptUrl = null);
