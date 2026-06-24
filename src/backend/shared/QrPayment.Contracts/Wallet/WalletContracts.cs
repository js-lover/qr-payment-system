// QrPayment.Contracts / Wallet / WalletContracts.cs
//
// WalletService HTTP endpoint'lerinin DTO'ları.
// Bu kontratları hem WalletService (sunucu) hem de
// TransactionService (iç çağrı yapan) ve mobil uygulama (istemci) kullanır.
//
// Kritik tasarım kararı:
//   Provision akışı iki adımlıdır:
//   1. provision/create → bakiyeden tutar bloke edilir (BLOCKED)
//   2. provision/{id}/confirm veya /release → bloke ya kesinleşir ya iade edilir
//   Bu sayede ödeme başarısız olursa bakiye otomatik serbest bırakılır.

namespace QrPayment.Contracts.Wallet;

// ─── Balance ─────────────────────────────────────────────────────────────────

/// <summary>Cüzdan bakiye sorgusunun yanıtı.</summary>
public record BalanceResponse
{
    public Guid WalletId { get; init; }

    /// <summary>Kullanılabilir bakiye (bloke tutarlar düşülmüş).</summary>
    public decimal AvailableBalance { get; init; }

    /// <summary>Bekleyen ödemeler için bloke edilen tutar.</summary>
    public decimal BlockedBalance { get; init; }

    /// <summary>Toplam bakiye = AvailableBalance + BlockedBalance.</summary>
    public decimal TotalBalance { get; init; }

    public string Currency { get; init; } = "TRY";
    public DateTimeOffset AsOf { get; init; } = DateTimeOffset.UtcNow;
}

// ─── Top-up ──────────────────────────────────────────────────────────────────

/// <summary>Cüzdana para yükleme isteği.</summary>
public record TopupRequest
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";

    /// <summary>Yükleme yöntemi: "CREDIT_CARD" | "EFT" | "HAVALE"</summary>
    public string Method { get; init; } = string.Empty;
}

/// <summary>Para yükleme isteğinin yanıtı.</summary>
public record TopupResponse
{
    public Guid TopupId { get; init; }

    /// <summary>Yükleme işleminin durumu: "PENDING" | "COMPLETED" | "FAILED"</summary>
    public string Status { get; init; } = string.Empty;

    public decimal NewBalance { get; init; }
}

// ─── Provision ───────────────────────────────────────────────────────────────

/// <summary>
/// QR ödeme için bakiyeden tutar bloke etme isteği.
/// TransactionService, müşteri QR'ı taradıktan sonra bu endpoint'i çağırır.
/// </summary>
public record ProvisionRequest
{
    public Guid WalletId { get; init; }

    /// <summary>Hangi QR işlemiyle ilişkili olduğu — idempotency için.</summary>
    public Guid QrToken { get; init; }

    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";

    /// <summary>Provision'ın geçerlilik bitiş zamanı (QR TTL + buffer).</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Bloke başarıyla oluşturulduğunda dönen yanıt.</summary>
public record ProvisionResponse
{
    public Guid ProvisionId { get; init; }

    /// <summary>Bloke edilen tutar.</summary>
    public decimal BlockedAmount { get; init; }

    /// <summary>Bloke sonrası kalan kullanılabilir bakiye.</summary>
    public decimal RemainingBalance { get; init; }
}

/// <summary>
/// Provision'ı kesinleştirme isteği.
/// ISO 8583 "00" yanıtı gelince TransactionService bu endpoint'i çağırır.
/// Ledger'a DEBIT kaydı düşülür, bloke kapatılır.
/// </summary>
public record ConfirmProvisionRequest
{
    public Guid ProvisionId { get; init; }
    public Guid TransactionId { get; init; }
}

/// <summary>
/// Provision'ı serbest bırakma isteği.
/// Ödeme başarısız veya timeout olduğunda TransactionService çağırır.
/// Bakiye eski haline döner.
/// </summary>
public record ReleaseProvisionRequest
{
    public Guid ProvisionId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
