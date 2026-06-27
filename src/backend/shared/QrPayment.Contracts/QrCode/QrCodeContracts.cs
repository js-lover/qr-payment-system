// QrPayment.Contracts / QrCode / QrCodeContracts.cs
//
// QrCodeService HTTP endpoint'lerinin DTO'ları.
// QR token'ın içinde finansal bilgi BULUNMAZ — sadece UUID taşır.
// Finansal bilgi Redis'te token ile ilişkili olarak saklanır.
//
// Akış:
//   Terminal → POST /qr/generate → { token: uuid, qrContent: "QRPAY:uuid" }
//   Terminal QR'ı ekranda gösterir.
//   Müşteri okur → GET /qr/{token}/validate → tutar + işyeri bilgisi
//   Müşteri onaylar → TransactionService devralır.

namespace QrPayment.Contracts.QrCode;

/// <summary>
/// POS terminal tarafından QR üretme isteği.
/// TerminalId, MerchantId, MerchantTitle: terminal tarafından yapılandırmadan alınır.
/// </summary>
public record GenerateQrRequest(
    Guid TerminalId,
    Guid MerchantId,
    string MerchantTitle,
    decimal Amount,
    string Currency = "TRY");

/// <summary>QR üretme yanıtı. Terminal bu bilgilerle QR kodunu oluşturur ve ekranda gösterir.</summary>
public record GenerateQrResponse(
    string Token,
    string QrContent,
    DateTimeOffset ExpiresAt,
    int RemainingSeconds,
    decimal Amount,
    string MerchantTitle);

/// <summary>
/// Müşteri QR'ı okuduktan sonra /qr/{token}/validate çağrısının yanıtı.
/// İstemci bu bilgileri onay ekranında kullanıcıya gösterir.
/// </summary>
public record ValidateQrResponse(
    string Token,
    Guid TerminalId,
    Guid MerchantId,
    string MerchantTitle,
    decimal Amount,
    string Status,
    DateTimeOffset ExpiresAt,
    int RemainingSeconds);

/// <summary>QR durumu sorgusunun yanıtı. POS terminal polling için kullanır.</summary>
public record QrStatusResponse(
    string Token,
    string Status,
    int RemainingSeconds);
