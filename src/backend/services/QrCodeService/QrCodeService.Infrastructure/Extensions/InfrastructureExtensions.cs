// QrCodeService.Infrastructure / Extensions / InfrastructureExtensions.cs
//
// QR Code Service DI kayıtları.
// Program.cs'te: builder.Services.AddInfrastructure(builder.Configuration)
//
// Kayıtlar:
//   - IConnectionMultiplexer → Redis bağlantısı (Singleton)
//   - IQrTokenService → QrTokenService (Singleton, Redis bağlantısı paylaşılır)

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QrCodeService.Infrastructure.Services;
using StackExchange.Redis;

namespace QrCodeService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ─── Redis ───────────────────────────────────────────────────────────
        // IConnectionMultiplexer thread-safe ve pahalıdır; Singleton olarak kaydedilir
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        // ─── QR Token Servisi ─────────────────────────────────────────────────
        services.AddSingleton<IQrTokenService, QrTokenService>();

        return services;
    }
}
