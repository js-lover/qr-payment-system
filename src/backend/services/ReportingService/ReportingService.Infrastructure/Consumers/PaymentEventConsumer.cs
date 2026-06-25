// ReportingService.Infrastructure / Consumers / PaymentEventConsumer.cs
//
// Kafka consumer — payment.completed ve payment.failed event'lerini dinler.
//
// payment.completed geldiğinde:
//   1. Elasticsearch'e işlem belgesi yazar (analitik ve arama için)
//   2. FCM push notification gönderir (müşteri mobil uygulamasına)
//
// payment.failed geldiğinde:
//   1. Elasticsearch'e başarısız işlem kaydı yazar
//
// Not: Bu servis PaymentCompletedEvent ve PaymentFailedEvent'i aynı consumer group
// altında dinler. Offset commit manuel yapılır (EnableAutoCommit = false).

using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QrPayment.Kafka;
using QrPayment.Kafka.Consumer;
using QrPayment.Kafka.Events;
using ReportingService.Domain.Models;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Infrastructure.Consumers;

public class PaymentCompletedConsumer(
    IConsumer<string, string> consumer,
    ILogger<PaymentCompletedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<PaymentCompletedEvent>(consumer, logger, Topics.PaymentCompleted)
{
    protected override async Task HandleAsync(PaymentCompletedEvent @event, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var esService = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();
        var fcmService = scope.ServiceProvider.GetRequiredService<IFcmService>();

        // Elasticsearch'e işlem belgesi yaz
        var doc = new TransactionDocument
        {
            Id = @event.TransactionId.ToString(),
            TransactionId = @event.TransactionId,
            MerchantId = @event.MerchantId,
            MerchantTitle = @event.MerchantTitle,
            Amount = @event.Amount,
            Currency = @event.Currency,
            Status = "COMPLETED",
            IsoResponseCode = @event.IsoRespCode,
            Stan = @event.Stan,
            Rrn = @event.Rrn,
            OccurredAt = @event.CompletedAt
        };
        await esService.IndexTransactionAsync(doc, ct);

        // FCM push bildirim (opsiyonel — FCM token yoksa atla)
        if (!string.IsNullOrEmpty(@event.FcmToken))
        {
            await fcmService.SendPaymentNotificationAsync(
                @event.FcmToken,
                title: "Ödeme Başarılı",
                body: $"{@event.MerchantTitle} - {@event.Amount:F2} {@event.Currency}",
                ct);
        }

        logger.LogInformation("Payment completed indexed. TransactionId={TransactionId}", @event.TransactionId);
    }
}

public class PaymentFailedConsumer(
    IConsumer<string, string> consumer,
    ILogger<PaymentFailedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<PaymentFailedEvent>(consumer, logger, Topics.PaymentFailed)
{
    protected override async Task HandleAsync(PaymentFailedEvent @event, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var esService = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();

        var doc = new TransactionDocument
        {
            Id = @event.TransactionId.ToString(),
            TransactionId = @event.TransactionId,
            MerchantId = @event.MerchantId,
            Amount = @event.Amount,
            Currency = @event.Currency,
            Status = "FAILED",
            IsoResponseCode = @event.IsoRespCode,
            OccurredAt = @event.FailedAt
        };
        await esService.IndexTransactionAsync(doc, ct);

        logger.LogInformation("Payment failed indexed. TransactionId={TransactionId}", @event.TransactionId);
    }
}
