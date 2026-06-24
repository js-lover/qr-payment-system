// QrPayment.Kafka / Consumer / KafkaConsumerBase.cs
//
// Kafka'dan mesaj okuyan servisler için abstract base class.
// IHostedService implement eder; uygulama başladığında otomatik devreye girer.
//
// Her servis bu sınıfı miras alır ve HandleAsync metodunu override ederek
// kendi işleme mantığını yazar. Base class:
//   - Consumer loop'u yönetir
//   - Deserialize eder
//   - Hata durumunda loglar (commit etmez — offset retry'a düşer)
//   - CancellationToken'a duyarlıdır (graceful shutdown)
//
// Kullanım örneği (WalletService):
//   public class CustomerRegisteredConsumer
//       : KafkaConsumerBase<CustomerRegisteredEvent>
//   {
//       public CustomerRegisteredConsumer(...) : base(..., Topics.CustomerRegistered) { }
//       protected override async Task HandleAsync(CustomerRegisteredEvent e, CancellationToken ct)
//           => await _walletService.CreateWalletAsync(e.CustomerId, ct);
//   }

using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QrPayment.Kafka.Events;

namespace QrPayment.Kafka.Consumer;

public abstract class KafkaConsumerBase<TEvent>(
    IConsumer<string, string> consumer,
    ILogger logger,
    string topic)
    : BackgroundService
    where TEvent : BaseEvent
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Topic'e subscribe ol
        consumer.Subscribe(topic);
        logger.LogInformation("Kafka consumer started. Topic={Topic} EventType={EventType}",
            topic, typeof(TEvent).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Blocking consume — her 1 saniyede timeout ile döner (shutdown için)
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                TEvent? @event;
                try
                {
                    @event = JsonSerializer.Deserialize<TEvent>(result.Message.Value, JsonOptions);
                }
                catch (JsonException ex)
                {
                    // Bozuk mesaj — dead-letter queue ileriki aşamada eklenebilir
                    logger.LogError(ex, "Failed to deserialize Kafka message. Topic={Topic} Offset={Offset}",
                        topic, result.Offset.Value);
                    consumer.Commit(result);
                    continue;
                }

                if (@event is null) { consumer.Commit(result); continue; }

                try
                {
                    await HandleAsync(@event, stoppingToken);
                    consumer.Commit(result); // Başarıyla işlendi, offset ilerlet
                }
                catch (Exception ex)
                {
                    // İşleme hatası — offset commit edilmez, mesaj yeniden okunur
                    logger.LogError(ex, "Error handling event. EventType={EventType} EventId={EventId}",
                        @event.EventType, @event.EventId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // Graceful shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Kafka consumer loop error. Topic={Topic}", topic);
                await Task.Delay(1000, stoppingToken); // Kısa bekleme, sonra tekrar dene
            }
        }

        consumer.Close();
        logger.LogInformation("Kafka consumer stopped. Topic={Topic}", topic);
    }

    /// <summary>Alt sınıflar bu metodu override ederek event işleme mantığını yazar.</summary>
    protected abstract Task HandleAsync(TEvent @event, CancellationToken ct);
}
