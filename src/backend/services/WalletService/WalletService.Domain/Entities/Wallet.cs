// WalletService.Domain / Entities / Wallet.cs
//
// Cüzdan entity'si — müşteri veya işyeri bakiyesini temsil eder.
//
// Çift taraflı muhasebe (double-entry bookkeeping) prensibi:
//   Her bakiye hareketi WalletLedger tablosuna yeni bir satır EKLER.
//   Mevcut satırlar asla güncellenmez veya silinmez.
//   Anlık bakiye, ledger kayıtlarının SUM'u ile hesaplanır.
//   Bu yaklaşım tam denetim izi (audit trail) ve tutarsızlık tespiti sağlar.
//
// Provision (rezervasyon) akışı:
//   1. ProvisionAsync → BlockedBalance artar, AvailableBalance azalır
//   2. ConfirmProvisionAsync → BlockedBalance azalır (işlem tamamlandı)
//   3. ReleaseProvisionAsync → BlockedBalance azalır, AvailableBalance artar (iptal)
//
// Race condition koruması:
//   Tüm bakiye değişiklikleri UPDLOCK + ROWLOCK ile kilitlenir.
//   Bkz: WalletRepository.GetByIdWithLockAsync

namespace WalletService.Domain.Entities;

public class Wallet
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Cüzdan sahibi (müşteri veya işyeri). Nullable — wallet sahipsiz açılabilir (KYC sürecinde).</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Sahip tipi: CUSTOMER | MERCHANT</summary>
    public string OwnerType { get; private set; } = "CUSTOMER";

    /// <summary>ISO 4217 para birimi kodu. Örn: "TRY", "USD".</summary>
    public string Currency { get; private set; } = "TRY";

    /// <summary>
    /// Kullanılabilir bakiye (kuruş cinsinden).
    /// Para birimi virgüllü değil tam sayı tutulur (örn. 100 = 1.00 TL).
    /// Bu sayede floating point hatalarından kaçınılır.
    /// </summary>
    public long AvailableBalance { get; private set; }

    /// <summary>Provizyon altındaki bloke bakiye (işlem devam ederken ayrılmış tutar).</summary>
    public long BlockedBalance { get; private set; }

    /// <summary>Toplam bakiye = AvailableBalance + BlockedBalance</summary>
    public long TotalBalance => AvailableBalance + BlockedBalance;

    /// <summary>KYC onaylandıktan sonra aktive edilir.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation property — ledger kayıtları
    public ICollection<WalletLedger> LedgerEntries { get; private set; } = [];

    private Wallet() { }

    public static Wallet Create(Guid ownerId, string ownerType = "CUSTOMER", string currency = "TRY")
        => new() { OwnerId = ownerId, OwnerType = ownerType, Currency = currency };

    /// <summary>KYC onaylandığında WalletService bu metodu çağırır.</summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// Bakiyeye para ekler (topup, gelen transfer).
    /// Sadece aktif cüzdanlarda çalışır.
    /// </summary>
    public void Credit(long amount)
    {
        if (!IsActive) throw new InvalidOperationException("Wallet is not active.");
        if (amount <= 0) throw new ArgumentException("Credit amount must be positive.");
        AvailableBalance += amount;
    }

    /// <summary>
    /// QR ödeme için tutar rezerve eder (bloke).
    /// AvailableBalance yeterliyse BlockedBalance'a aktarır.
    /// </summary>
    public void Provision(long amount)
    {
        if (!IsActive) throw new InvalidOperationException("Wallet is not active.");
        if (amount <= 0) throw new ArgumentException("Provision amount must be positive.");
        if (AvailableBalance < amount) throw new InvalidOperationException("Insufficient balance.");

        AvailableBalance -= amount;
        BlockedBalance += amount;
    }

    /// <summary>
    /// Başarılı ödeme sonrası bloke tutarı confirm eder (düşer).
    /// Tutar işyerine transfer sistematiği dışarıda yönetilir.
    /// </summary>
    public void ConfirmProvision(long amount)
    {
        if (BlockedBalance < amount) throw new InvalidOperationException("Blocked balance insufficient.");
        BlockedBalance -= amount;
    }

    /// <summary>
    /// Başarısız veya iptal ödeme sonrası bloke tutarı serbest bırakır.
    /// </summary>
    public void ReleaseProvision(long amount)
    {
        if (BlockedBalance < amount) throw new InvalidOperationException("Blocked balance insufficient.");
        BlockedBalance -= amount;
        AvailableBalance += amount;
    }
}

/// <summary>
/// Değişmez muhasebe kaydı (immutable ledger).
/// Hiçbir zaman UPDATE veya DELETE yapılmaz.
/// Her hareket yeni bir satır olarak eklenir.
/// </summary>
public class WalletLedger
{
    public long Id { get; private set; }
    public Guid WalletId { get; private set; }

    /// <summary>Hareket tipi: TOPUP | PROVISION | CONFIRM | RELEASE | TRANSFER_IN | TRANSFER_OUT</summary>
    public string EntryType { get; private set; } = string.Empty;

    /// <summary>Tutar (kuruş cinsinden). Pozitif = giriş, negatif = çıkış.</summary>
    public long Amount { get; private set; }

    /// <summary>Bu hareketin gerçekleşmesi sonrası cüzdanın anlık bakiyesi.</summary>
    public long BalanceAfter { get; private set; }

    /// <summary>İlgili QR token, işlem ID vb. harici referans.</summary>
    public string? ReferenceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private WalletLedger() { }

    public static WalletLedger Create(Guid walletId, string entryType, long amount, long balanceAfter, string? referenceId = null)
        => new()
        {
            WalletId = walletId,
            EntryType = entryType,
            Amount = amount,
            BalanceAfter = balanceAfter,
            ReferenceId = referenceId
        };
}
