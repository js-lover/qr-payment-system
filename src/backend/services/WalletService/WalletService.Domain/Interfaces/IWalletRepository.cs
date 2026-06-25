// WalletService.Domain / Interfaces / IWalletRepository.cs
//
// Cüzdan repository arayüzü.
//
// GetByOwnerIdWithLockAsync:
//   UPDLOCK + ROWLOCK SQL ipuçlarıyla kilitleme yapılır.
//   Bu metot sadece provision/confirm/release işlemlerinde kullanılır.
//   Eş zamanlı iki ödeme isteği aynı cüzdana yazarsa biri bekler, her ikisi de başarılı olmaz.
//
// AddLedgerEntryAsync:
//   Her bakiye değişikliği için çağrılır. Kayıt asla değiştirilmez (immutable).

using WalletService.Domain.Entities;

namespace WalletService.Domain.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Cüzdanı UPDLOCK + ROWLOCK ile kilitleyerek getirir.
    /// Race condition önlemek için provision/confirm/release işlemlerinde kullanılır.
    /// Transaction scope içinde çağrılmalıdır.
    /// </summary>
    Task<Wallet?> GetByOwnerIdWithLockAsync(Guid ownerId, CancellationToken ct = default);

    Task AddAsync(Wallet wallet, CancellationToken ct = default);
    Task AddLedgerEntryAsync(WalletLedger entry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
