// OnboardingService.Infrastructure / Extensions / InfrastructureExtensions.cs
//
// Dependency Injection kayıtlarını merkezi olarak yönetir.
// Program.cs'te tek satırla tüm bağımlılıkları kaydeder: builder.Services.AddInfrastructure(builder.Configuration)
//
// Kayıtlar:
//   - OnboardingDbContext (MSSQL, EF Core)
//   - ICustomerRepository, IMerchantRepository, ITerminalRepository, IOtpRepository → Repository impl.
//   - ISmsService → Dev: LogSmsService, Prod: NetgsmSmsService (HttpClient ile)
//   - IOnboardingService → OnboardingApplicationService
//   - IKafkaProducer → KafkaProducer (Singleton, thread-safe)

using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnboardingService.Domain.Interfaces;
using OnboardingService.Infrastructure.Persistence;
using OnboardingService.Infrastructure.Persistence.Repositories;
using OnboardingService.Infrastructure.Services;
using QrPayment.Kafka.Producer;

namespace OnboardingService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ─── Veritabanı ──────────────────────────────────────────────────────
        services.AddDbContext<OnboardingDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("OnboardingDb"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        // ─── Repository'ler ──────────────────────────────────────────────────
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddScoped<ITerminalRepository, TerminalRepository>();
        services.AddScoped<IOtpRepository, OtpRepository>();

        // ─── SMS Servisi ──────────────────────────────────────────────────────
        // Dev'de gerçek SMS gönderilmez; OTP yalnızca loglara yazılır
        var smsProvider = configuration["Sms:Provider"] ?? "Log";
        if (smsProvider.Equals("Netgsm", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ISmsService, NetgsmSmsService>();
        }
        else
        {
            services.AddScoped<ISmsService, LogSmsService>();
        }

        // ─── Kafka Producer ───────────────────────────────────────────────────
        services.AddSingleton<IKafkaProducer>(sp =>
        {
            var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
            var producer = new ProducerBuilder<string, string>(
                new ProducerConfig { BootstrapServers = bootstrapServers }).Build();
            var logger = sp.GetRequiredService<ILogger<KafkaProducer>>();
            return new KafkaProducer(producer, logger);
        });

        // ─── Onboarding Servis ────────────────────────────────────────────────
        services.AddScoped<IOnboardingService, OnboardingApplicationService>();

        return services;
    }

    /// <summary>
    /// Dev/Staging ortamlarında DB migration'larını otomatik uygular.
    /// Prod'da çalıştırılmamalı — üretimde migration manuel veya CI/CD ile yapılır.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OnboardingDbContext>();
        await db.Database.MigrateAsync();
    }
}
