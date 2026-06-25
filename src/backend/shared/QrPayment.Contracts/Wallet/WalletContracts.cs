// QrPayment.Contracts / Wallet / WalletContracts.cs
//
// WalletService HTTP endpoint'lerinin DTO'ları.
// Bu kontratları hem WalletService (sunucu) hem de
// TransactionService (dahili çağrı) ve mobil uygulama (istemci) kullanır.
//
// Bakiye kuralı:
//   Tutarlar TL/decimal olarak API'den alınır;
//   servis içinde kuruşa (×100, long) çevrilir.
//   Bu sayede floating point hataları önlenir.
//
// Provision akışı (iki adım):
//   1. POST /wallet/provision   → tutar bloke edilir
//   2. POST /wallet/confirm     → bloke kesinleşir (ödeme başarılı)
//      POST /wallet/release     → bloke serbest bırakılır (ödeme başarısız/iptal)

namespace QrPayment.Contracts.Wallet;

// ─── Balance ─────────────────────────────────────────────────────────────────

/// <summary>Cüzdan bakiye sorgusunun yanıtı.</summary>
public record BalanceResponse(
    decimal AvailableBalance,
    decimal BlockedBalance,
    decimal TotalBalance,
    string Currency,
    DateTimeOffset AsOf);

// ─── Top-up ──────────────────────────────────────────────────────────────────

/// <summary>
/// Cüzdana para yükleme isteği.
/// ReferenceId: banka EFT referans numarası veya kredi kartı işlem ID'si.
/// </summary>
public record TopupRequest(decimal Amount, string ReferenceId, string Currency = "TRY");

// ─── Provision ───────────────────────────────────────────────────────────────

/// <summary>
/// QR ödeme için bakiyeden tutar bloke etme isteği.
/// TransactionService, müşteri QR'ı taradıktan sonra bu endpoint'i çağırır.
/// </summary>
public record ProvisionRequest(decimal Amount, string QrToken, string Currency = "TRY");

/// <summary>
/// Başarılı ödeme sonrası blokajı kesinleştirir.
/// TransactionService, ISO 8583 "00" yanıtı alınca çağırır.
/// </summary>
public record ConfirmProvisionRequest(decimal Amount, string TransactionId);

/// <summary>
/// Başarısız/iptal ödeme sonrası blokajı serbest bırakır.
/// Bakiye eski haline döner.
/// </summary>
public record ReleaseProvisionRequest(decimal Amount, string QrToken);
