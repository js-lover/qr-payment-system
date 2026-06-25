// WalletService.Infrastructure / Persistence / WalletDbContext.cs
//
// EF Core DbContext — wallet_db veritabanı.
// Tablolar: wallets, wallet_ledger
//
// wallet_ledger tablosu:
//   - Birincil anahtar BIGINT IDENTITY — sıralı, verimli indeksleme
//   - wallet_id + created_at üzerinde bileşik indeks: bakiye özeti sorguları için
//   - Hiçbir zaman DELETE veya UPDATE yürütülmez (immutable ledger prensibi)

using Microsoft.EntityFrameworkCore;
using WalletService.Domain.Entities;

namespace WalletService.Infrastructure.Persistence;

public class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletLedger> WalletLedgers => Set<WalletLedger>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // ─── Wallet ───────────────────────────────────────────────────────────
        builder.Entity<Wallet>(e =>
        {
            e.ToTable("wallets");
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(w => w.OwnerId).IsRequired();
            e.Property(w => w.OwnerType).HasMaxLength(20).HasDefaultValue("CUSTOMER");
            e.Property(w => w.Currency).HasMaxLength(3).HasDefaultValue("TRY");
            e.Property(w => w.AvailableBalance).HasDefaultValue(0L);
            e.Property(w => w.BlockedBalance).HasDefaultValue(0L);
            e.Property(w => w.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Sahip başına bir cüzdan (para birimi bazlı genişletme için OwnerId+Currency kullanılabilir)
            e.HasIndex(w => w.OwnerId).IsUnique().HasDatabaseName("IX_wallets_owner_id");

            // Navigation: ledger kayıtları
            e.HasMany(w => w.LedgerEntries).WithOne().HasForeignKey(l => l.WalletId);
        });

        // ─── WalletLedger ─────────────────────────────────────────────────────
        builder.Entity<WalletLedger>(e =>
        {
            e.ToTable("wallet_ledger");
            e.HasKey(l => l.Id);
            // BIGINT IDENTITY — sıralı ID, yüksek hacimli insert için en uygun
            e.Property(l => l.Id).UseIdentityColumn();
            e.Property(l => l.EntryType).HasMaxLength(30).IsRequired();
            e.Property(l => l.ReferenceId).HasMaxLength(100);
            e.Property(l => l.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Bakiye özeti ve extract sorguları için bileşik indeks
            e.HasIndex(l => new { l.WalletId, l.CreatedAt })
             .HasDatabaseName("IX_wallet_ledger_wallet_id_created_at");
        });
    }
}
