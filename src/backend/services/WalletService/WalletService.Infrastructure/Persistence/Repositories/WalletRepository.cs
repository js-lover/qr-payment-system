// WalletService.Infrastructure / Persistence / Repositories / WalletRepository.cs
//
// IWalletRepository'nin EF Core implementasyonu.
//
// GetByOwnerIdWithLockAsync:
//   Raw SQL ile UPDLOCK + ROWLOCK ipuçları kullanılır.
//   EF Core bu SQL ipuçlarını doğrudan desteklemediğinden FromSqlRaw kullanılır.
//   Bu yöntem SQL Server'a özgüdür; taşıma gerekirse farklı DB için değiştirilmeli.
//
//   UPDLOCK: Okuma kilitlemesini güncelleme kilidi seviyesine yükseltir.
//            Aynı satırı aynı anda iki transaction okuyamaz.
//   ROWLOCK: Sayfa veya tablo yerine yalnızca satır kilitlenir (verimlilik için).

using Microsoft.EntityFrameworkCore;
using WalletService.Domain.Entities;
using WalletService.Domain.Interfaces;

namespace WalletService.Infrastructure.Persistence.Repositories;

public class WalletRepository(WalletDbContext db) : IWalletRepository
{
    public Task<Wallet?> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
        => db.Wallets.FirstOrDefaultAsync(w => w.OwnerId == ownerId, ct);

    /// <summary>
    /// UPDLOCK + ROWLOCK kilitleme ile cüzdan getirir.
    /// Eş zamanlı provision isteklerinde yarış koşulunu (race condition) önler.
    /// Mutlaka bir transaction scope içinde çağrılmalıdır.
    /// </summary>
    public Task<Wallet?> GetByOwnerIdWithLockAsync(Guid ownerId, CancellationToken ct = default)
        => db.Wallets
             .FromSqlRaw("SELECT * FROM wallets WITH (UPDLOCK, ROWLOCK) WHERE owner_id = {0}", ownerId)
             .FirstOrDefaultAsync(ct);

    public async Task AddAsync(Wallet wallet, CancellationToken ct = default)
        => await db.Wallets.AddAsync(wallet, ct);

    public async Task AddLedgerEntryAsync(WalletLedger entry, CancellationToken ct = default)
        => await db.WalletLedgers.AddAsync(entry, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
