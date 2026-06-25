// OnboardingService.Infrastructure / Persistence / OnboardingDbContext.cs
//
// EF Core DbContext — onboarding_db veritabanı.
// Tablolar: customers, merchants, branches, terminals, otp_verifications

using Microsoft.EntityFrameworkCore;
using OnboardingService.Domain.Entities;

namespace OnboardingService.Infrastructure.Persistence;

public class OnboardingDbContext(DbContextOptions<OnboardingDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // ─── Customer ─────────────────────────────────────────────────────────
        builder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(c => c.IdentityHash).HasMaxLength(256).IsRequired();
            e.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
            e.Property(c => c.LastName).HasMaxLength(100).IsRequired();
            e.Property(c => c.Gsm).HasMaxLength(20).IsRequired();
            e.Property(c => c.KycStatus).HasMaxLength(50).HasDefaultValue("PENDING");
            e.Property(c => c.FcmToken).HasMaxLength(512);
            e.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Login/lookup için hızlı erişim
            e.HasIndex(c => c.Gsm).IsUnique().HasDatabaseName("IX_customers_gsm");
            e.HasIndex(c => c.IdentityHash).IsUnique().HasDatabaseName("IX_customers_identity_hash");
        });

        // ─── Merchant ─────────────────────────────────────────────────────────
        builder.Entity<Merchant>(e =>
        {
            e.ToTable("merchants");
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(m => m.Title).HasMaxLength(200).IsRequired();
            e.Property(m => m.TaxNumber).HasMaxLength(20).IsRequired();
            e.Property(m => m.Iban).HasMaxLength(50).IsRequired();
            e.Property(m => m.Mcc).HasMaxLength(10).IsRequired();
            e.Property(m => m.Status).HasMaxLength(50).HasDefaultValue("PENDING");
            e.Property(m => m.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            e.HasIndex(m => m.TaxNumber).IsUnique().HasDatabaseName("IX_merchants_tax_number");
            e.HasMany(m => m.Branches).WithOne().HasForeignKey(b => b.MerchantId);
        });

        // ─── Branch ───────────────────────────────────────────────────────────
        builder.Entity<Branch>(e =>
        {
            e.ToTable("branches");
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(b => b.Name).HasMaxLength(200).IsRequired();
            e.Property(b => b.Address).HasMaxLength(500);
            e.HasIndex(b => b.MerchantId).HasDatabaseName("IX_branches_merchant_id");
        });

        // ─── Terminal ─────────────────────────────────────────────────────────
        builder.Entity<Terminal>(e =>
        {
            e.ToTable("terminals");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasMaxLength(50).IsRequired();
            e.Property(t => t.SecretKey).HasMaxLength(256).IsRequired();
            e.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ─── OtpVerification ──────────────────────────────────────────────────
        builder.Entity<OtpVerification>(e =>
        {
            e.ToTable("otp_verifications");
            e.HasKey(o => o.Id);
            e.Property(o => o.Gsm).HasMaxLength(20).IsRequired();
            e.Property(o => o.OtpCode).HasMaxLength(10).IsRequired();
            e.Property(o => o.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // GSM bazlı OTP lookup için indeks
            e.HasIndex(o => new { o.Gsm, o.IsUsed }).HasDatabaseName("IX_otp_gsm_active");
        });
    }
}
