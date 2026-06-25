// WalletService.Infrastructure / Extensions / InfrastructureExtensions.cs
//
// Wallet Service DI kayıtları.
// Program.cs'te: builder.Services.AddInfrastructure(builder.Configuration)
//
// Kayıtlar:
//   - WalletDbContext (MSSQL)
//   - IWalletRepository → WalletRepository (Scoped)
//   - IWalletService → WalletApplicationService (Scoped)
//   - CustomerKycApprovedConsumer (Hosted Service, BackgroundService)
//   - Kafka IConsumer<string,string> (Singleton)

using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WalletService.Domain.Interfaces;
using WalletService.Infrastructure.Consumers;
using WalletService.Infrastructure.Persistence;
using WalletService.Infrastructure.Persistence.Repositories;
using WalletService.Infrastructure.Services;

namespace WalletService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ─── Veritabanı ──────────────────────────────────────────────────────
        services.AddDbContext<WalletDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("WalletDb"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        // ─── Repository ──────────────────────────────────────────────────────
        services.AddScoped<IWalletRepository, WalletRepository>();

        // ─── Wallet Servisi ───────────────────────────────────────────────────
        services.AddScoped<IWalletService, WalletApplicationService>();

        // ─── Kafka Consumer (BackgroundService) ───────────────────────────────
        // Tek bir IConsumer instance tüm consumer'lar tarafından paylaşılmaz;
        // her consumer kendi consumer instance'ına sahip olmalı
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var groupId = configuration["Kafka:GroupId"] ?? "wallet-service";

        services.AddSingleton<IConsumer<string, string>>(sp =>
            new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            }).Build());

        services.AddHostedService<CustomerKycApprovedConsumer>();

        return services;
    }

    /// <summary>Dev/Staging ortamlarında migration'ları otomatik uygular.</summary>
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
        await db.Database.MigrateAsync();
    }
}
