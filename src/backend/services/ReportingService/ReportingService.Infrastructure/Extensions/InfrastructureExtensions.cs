// ReportingService.Infrastructure / Extensions / InfrastructureExtensions.cs
//
// Reporting Service DI kayıtları.
// Program.cs'te: builder.Services.AddInfrastructure(builder.Configuration)
//
// Kayıtlar:
//   - ElasticsearchClient (Singleton) → IElasticsearchService
//   - IFcmService → Dev: LogFcmService, Prod: FirebaseFcmService
//   - PaymentCompletedConsumer (Hosted Service)
//   - PaymentFailedConsumer   (Hosted Service)
//   - Kafka IConsumer instances (her consumer için ayrı instance)

using Confluent.Kafka;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportingService.Infrastructure.Consumers;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ─── Elasticsearch ────────────────────────────────────────────────────
        var esUrl = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
        var esClient = new ElasticsearchClient(new Uri(esUrl));
        services.AddSingleton(esClient);
        services.AddScoped<IElasticsearchService, ElasticsearchService>();

        // ─── FCM Servisi ──────────────────────────────────────────────────────
        var fcmProvider = configuration["Fcm:Provider"] ?? "Log";
        if (fcmProvider.Equals("Firebase", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<IFcmService, FirebaseFcmService>();
        else
            services.AddScoped<IFcmService, LogFcmService>();

        // ─── Kafka Consumers (her consumer kendi IConsumer instance'ına sahip) ─
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var groupId = configuration["Kafka:GroupId"] ?? "reporting-service";

        IConsumer<string, string> BuildConsumer() =>
            new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            }).Build();

        services.AddSingleton(_ => BuildConsumer());
        services.AddHostedService<PaymentCompletedConsumer>(sp =>
            new PaymentCompletedConsumer(BuildConsumer(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PaymentCompletedConsumer>>(),
                sp.GetRequiredService<IServiceScopeFactory>()));

        services.AddHostedService<PaymentFailedConsumer>(sp =>
            new PaymentFailedConsumer(BuildConsumer(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<PaymentFailedConsumer>>(),
                sp.GetRequiredService<IServiceScopeFactory>()));

        return services;
    }
}
