// AuthService.Infrastructure / Persistence / AuthDbContext.cs
//
// EF Core DbContext — auth_db veritabanı.
// Üç tablo yönetir: users, refresh_tokens, terminal_credentials.
//
// Migration üretmek için:
//   dotnet ef migrations add <MigrationName>
//     --project AuthService.Infrastructure
//     --startup-project AuthService.Api
//     --output-dir Migrations

using AuthService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TerminalCredential> TerminalCredentials => Set<TerminalCredential>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // ─── User ────────────────────────────────────────────────────────────
        builder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(u => u.Username).HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(u => u.Role).HasMaxLength(50).IsRequired();
            e.Property(u => u.TotpSecret).HasMaxLength(100);
            e.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Kullanıcı adı benzersiz olmalı (login için lookup)
            e.HasIndex(u => u.Username).IsUnique().HasDatabaseName("IX_users_username");
        });

        // ─── RefreshToken ────────────────────────────────────────────────────
        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(rt => rt.Id);
            e.Property(rt => rt.Token).HasMaxLength(512).IsRequired();
            e.Property(rt => rt.IsRevoked).HasDefaultValue(false);

            // Aktif token arama için — revoke edilmemiş token'lar filtrelenir
            e.HasIndex(rt => rt.Token)
             .HasFilter("[IsRevoked] = 0")
             .HasDatabaseName("IX_refresh_tokens_token_active");

            e.HasIndex(rt => rt.UserId).HasDatabaseName("IX_refresh_tokens_user_id");
        });

        // ─── TerminalCredential ───────────────────────────────────────────────
        builder.Entity<TerminalCredential>(e =>
        {
            e.ToTable("terminal_credentials");
            e.HasKey(tc => tc.TerminalId);
            e.Property(tc => tc.TerminalId).HasMaxLength(50).IsRequired();
            e.Property(tc => tc.SecretKey).HasMaxLength(256).IsRequired();
            e.Property(tc => tc.IsActive).HasDefaultValue(true);
            e.Property(tc => tc.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
