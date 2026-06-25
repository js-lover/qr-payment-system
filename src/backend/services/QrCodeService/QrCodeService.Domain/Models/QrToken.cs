// QrCodeService.Domain / Models / QrToken.cs
//
// QR ödeme token'ının domain modeli.
//
// Token yaşam döngüsü:
//   PENDING  → Token üretildi, ödeme bekleniyor
//   SCANNING → Mobil uygulama token'ı taradı (race condition penceresinde)
//   CLAIMED  → Ödeme başlatıldı, TransactionService işliyor
//   EXPIRED  → TTL (90 saniye) doldu, Redis'ten silindi
//
// Redis depolama:
//   Key   : "qr:{token}" (örn: "qr:a1b2c3d4-...")
//   Value : JSON serialize edilmiş QrToken
//   TTL   : 90 saniye
//
// SETNX (SetNotExists) kullanımı:
//   Token claim edilirken SETNX ile "qr:{token}:claimed" anahtarı set edilir.
//   Aynı anda iki ödeme isteği gelirse yalnızca biri başarılı olur.

namespace QrCodeService.Domain.Models;

public class QrToken
{
    public string Token { get; set; } = string.Empty;

    /// <summary>QR kod içeriği: "QRPAY:{uuid}" formatında. Müşteri kamerası bu değeri okur.</summary>
    public string QrContent { get; set; } = string.Empty;

    public Guid TerminalId { get; set; }
    public Guid MerchantId { get; set; }
    public string MerchantTitle { get; set; } = string.Empty;

    /// <summary>Ödeme tutarı (TL, decimal).</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "TRY";

    /// <summary>Token durumu: PENDING | SCANNING | CLAIMED | EXPIRED</summary>
    public string Status { get; set; } = "PENDING";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>TTL'den kalan saniye (Redis'ten sorgulanarak hesaplanır).</summary>
    public int RemainingSeconds { get; set; }
}
