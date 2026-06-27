// TransactionService.Infrastructure / Extensions / InfrastructureExtensions.cs
//
// Transaction Service DI kayıtları.
// Program.cs'te: builder.Services.AddInfrastructure(builder.Configuration)
//
// Kayıtlar:
//   - TransactionDbContext (MSSQL)
//   - ITransactionRepository → TransactionRepository
//   - IBankSimulatorClient → BankSimulatorClient
//   - IPaymentService → PaymentService
//   - IKafkaProducer → KafkaProducer (Singleton)
//   - HttpClient'lar: QrCodeService, WalletService (named clients)

using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionService.Domain.Interfaces;
using TransactionService.Infrastructure.Persistence;
using TransactionService.Infrastructure.Persistence.Repositories;
using TransactionService.Infrastructure.Services;
using QrPayment.Kafka.Producer;

namespace TransactionService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ─── Veritabanı ──────────────────────────────────────────────────────
        services.AddDbContext<TransactionDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("TransactionDb"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        // ─── Repository ──────────────────────────────────────────────────────
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        // ─── Bank Simulator (TCP istemci) ─────────────────────────────────────
        services.AddScoped<IBankSimulatorClient, BankSimulatorClient>();

        // ─── Ödeme Servisi ────────────────────────────────────────────────────
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddHttpContextAccessor();

        // ─── Kafka Producer ───────────────────────────────────────────────────
        services.AddSingleton<IKafkaProducer>(sp =>
        {
            var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
            var producer = new ProducerBuilder<string, string>(
                new ProducerConfig { BootstrapServers = bootstrapServers }).Build();
            var logger = sp.GetRequiredService<ILogger<KafkaProducer>>();
            return new KafkaProducer(producer, logger);
        });

        // ─── HttpClient'lar (Servis arası HTTP çağrıları) ─────────────────────
        var qrServiceUrl = configuration["Services:QrCodeService"] ?? "http://localhost:5004";
        services.AddHttpClient("QrCodeService",
            c => c.BaseAddress = new Uri(qrServiceUrl));

        var walletServiceUrl = configuration["Services:WalletService"] ?? "http://localhost:5003";
        services.AddHttpClient("WalletService",
            c => c.BaseAddress = new Uri(walletServiceUrl));

        return services;
    }

    /// <summary>Dev/Staging ortamlarında migration'ları otomatik uygular.</summary>
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        await db.Database.MigrateAsync();
    }
}
