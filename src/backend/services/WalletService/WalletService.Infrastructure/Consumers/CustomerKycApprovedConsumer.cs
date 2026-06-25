// WalletService.Infrastructure / Consumers / CustomerKycApprovedConsumer.cs
//
// Kafka consumer — customer.kyc_approved topic'ini dinler.
//
// Akış:
//   OnboardingService → [KYC onayı] → Kafka: customer.kyc_approved
//   WalletService bu event'i dinler → Wallet.Activate() çağırır
//   Cüzdan aktive olduktan sonra müşteri ödeme yapabilir
//
// KafkaConsumerBase<T>:
//   - BackgroundService olarak çalışır (hosted service)
//   - EnableAutoCommit = false → manuel offset commit
//   - HandleAsync başarısız olursa offset commit yapılmaz → mesaj tekrar işlenir

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Confluent.Kafka;
using QrPayment.Kafka.Consumer;
using QrPayment.Kafka.Events;
using WalletService.Infrastructure.Services;

namespace WalletService.Infrastructure.Consumers;

public class CustomerKycApprovedConsumer(
    IConsumer<string, string> consumer,
    ILogger<CustomerKycApprovedConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : KafkaConsumerBase<CustomerKycApprovedEvent>(consumer, logger, QrPayment.Kafka.Topics.CustomerKycApproved)
{
    protected override async Task HandleAsync(CustomerKycApprovedEvent @event, CancellationToken ct)
    {
        // Scoped service'i BackgroundService (singleton-scope) içinde çöz
        await using var scope = scopeFactory.CreateAsyncScope();
        var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();

        // Cüzdan yoksa oluştur, varsa aktive et
        try
        {
            await walletService.CreateWalletAsync(@event.CustomerId, "CUSTOMER", ct);
        }
        catch
        {
            // Cüzdan zaten varsa oluşturma adımını atla
        }

        await walletService.ActivateWalletAsync(@event.CustomerId, ct);

        logger.LogInformation("Wallet activated for customer {CustomerId}", @event.CustomerId);
    }
}
