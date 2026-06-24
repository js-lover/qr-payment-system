// AuthService.Infrastructure / Extensions / InfrastructureExtensions.cs
//
// Infrastructure katmanının tüm DI kayıtlarını tek noktada toplar.
// Program.cs sadece bu metodu çağırır; bağımlılıkların detayı burada.

using AuthService.Domain.Interfaces;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Persistence.Repositories;
using AuthService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ─── EF Core — MSSQL ──────────────────────────────────────────────
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("AuthDb"),
                sql => sql.MigrationsAssembly(typeof(AuthDbContext).Assembly.FullName)));

        // ─── Repositories ─────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITerminalCredentialRepository, TerminalCredentialRepository>();

        // ─── Domain Services ──────────────────────────────────────────────
        // Singleton: RSA anahtarları lazy-loaded ve cache'leniyor
        services.AddSingleton<IJwtService, JwtService>();
        services.AddSingleton<ITotpService, TotpService>();

        // Scoped: Her istek için yeni instance (HMAC nonce cache request scoped değil,
        // ama IMemoryCache singleton olduğu için HmacService scoped olabilir)
        services.AddScoped<IHmacService, HmacService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // In-memory cache — HMAC nonce replay koruması için
        // Production'da Redis ile değiştirilebilir
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Uygulama başlarken EF Core migration'larını otomatik uygular.
    /// Sadece dev/staging ortamında kullanılmalı; production'da migration
    /// CI/CD pipeline'ında ayrıca çalıştırılır.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.MigrateAsync();
    }
}
