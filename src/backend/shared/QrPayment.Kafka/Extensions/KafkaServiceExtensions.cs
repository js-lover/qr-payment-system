// QrPayment.Kafka / Extensions / KafkaServiceExtensions.cs
//
// Her servisin Program.cs'inde Kafka producer ve consumer'ı
// DI container'a kaydetmek için kullanılan uzantı metotları.
//
// Kullanım (Program.cs):
//   builder.Services.AddKafkaProducer(builder.Configuration);
//   builder.Services.AddKafkaConsumer<CustomerRegisteredEvent,
//       CustomerRegisteredConsumer>(builder.Configuration);

using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QrPayment.Kafka.Consumer;
using QrPayment.Kafka.Events;
using QrPayment.Kafka.Producer;

namespace QrPayment.Kafka.Extensions;

public static class KafkaServiceExtensions
{
    /// <summary>
    /// Confluent Kafka Producer'ı DI'ye kaydeder.
    /// appsettings.json'daki "Kafka:BootstrapServers" değerini okur.
    /// </summary>
    public static IServiceCollection AddKafkaProducer(
        this IServiceCollection services, IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
                               ?? "localhost:9092";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,      // En az broker leader'ı onaylamalı
            MessageTimeoutMs = 5000  // 5 saniye timeout
        };

        services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
        services.AddSingleton<IKafkaProducer, KafkaProducer>();

        return services;
    }

    /// <summary>
    /// Belirtilen event tipi için bir BackgroundService consumer kaydeder.
    /// Her consumer kendi group ID'sini appsettings'den alır.
    /// </summary>
    public static IServiceCollection AddKafkaConsumer<TEvent, TConsumer>(
        this IServiceCollection services, IConfiguration configuration)
        where TEvent : BaseEvent
        where TConsumer : KafkaConsumerBase<TEvent>
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var groupId = configuration["Kafka:GroupId"] ?? "default-group";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,  // Yeni grup ilk mesajdan başlar
            EnableAutoCommit = false  // Manuel commit — işleme garantisi için
        };

        services.AddSingleton(new ConsumerBuilder<string, string>(consumerConfig).Build());
        services.AddHostedService<TConsumer>();

        return services;
    }
}
