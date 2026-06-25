// TransactionService.Infrastructure / Persistence / TransactionDbContext.cs
//
// EF Core DbContext — transaction_db veritabanı.
// Tablo: transactions
//
// Tasarım notu:
//   İşlem kaydı oluşturulduğunda Status = "PROCESSING" dir.
//   ISO 8583 yanıtı geldiğinde Status güncellenir.
//   Bu sefer UPDATE yapılır (wallet_ledger'dan farklı) — işlemler değiştirilebilir kayıttır.
//   Ancak tamamlanmış işleme iade hariç ikinci kez dokunulmaz.

using Microsoft.EntityFrameworkCore;
using TransactionService.Domain.Entities;

namespace TransactionService.Infrastructure.Persistence;

public class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(t => t.QrToken).HasMaxLength(100).IsRequired();
            e.Property(t => t.Status).HasMaxLength(30).HasDefaultValue("PROCESSING");
            e.Property(t => t.Currency).HasMaxLength(3).HasDefaultValue("TRY");
            e.Property(t => t.IsoResponseCode).HasMaxLength(10);
            e.Property(t => t.Stan).HasMaxLength(20);
            e.Property(t => t.Rrn).HasMaxLength(30);
            e.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // QR token tekil işlem (bir QR bir kez kullanılır)
            e.HasIndex(t => t.QrToken).IsUnique().HasDatabaseName("IX_transactions_qr_token");

            // Müşteri ve merchant bazlı raporlama için
            e.HasIndex(t => t.CustomerId).HasDatabaseName("IX_transactions_customer_id");
            e.HasIndex(t => t.MerchantId).HasDatabaseName("IX_transactions_merchant_id");
        });
    }
}
