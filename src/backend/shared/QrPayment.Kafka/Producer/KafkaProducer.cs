// QrPayment.Kafka / Producer / KafkaProducer.cs
//
// Kafka'ya mesaj gönderen generic producer.
// Her domain event'i JSON'a serileştirip ilgili topic'e yazar.
//
// Retry politikası: Confluent.Kafka varsayılan ayarları (en fazla 3 deneme).
// Mesaj anahtarı (Key) olarak EventId kullanılır; aynı event idempotent işlenir.
//
// Kullanım:
//   var producer = serviceProvider.GetRequiredService<IKafkaProducer>();
//   await producer.PublishAsync(Topics.PaymentCompleted, new PaymentCompletedEvent { ... });

using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using QrPayment.Kafka.Events;

namespace QrPayment.Kafka.Producer;

public interface IKafkaProducer
{
    Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken ct = default)
        where TEvent : BaseEvent;
}

public class KafkaProducer(IProducer<string, string> producer, ILogger<KafkaProducer> logger)
    : IKafkaProducer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task PublishAsync<TEvent>(string topic, TEvent @event, CancellationToken ct = default)
        where TEvent : BaseEvent
    {
        var payload = JsonSerializer.Serialize(@event, JsonOptions);

        var message = new Message<string, string>
        {
            // EventId'yi key olarak kullanmak, aynı partition'da sıralı işlenmesini sağlar
            Key = @event.EventId,
            Value = payload
        };

        try
        {
            var result = await producer.ProduceAsync(topic, message, ct);
            logger.LogInformation(
                "Event published. Topic={Topic} EventType={EventType} Partition={Partition} Offset={Offset}",
                topic, @event.EventType, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogError(ex, "Failed to publish event. Topic={Topic} EventType={EventType}",
                topic, @event.EventType);
            throw;
        }
    }

    public void Dispose() => producer.Flush(TimeSpan.FromSeconds(5));
}
