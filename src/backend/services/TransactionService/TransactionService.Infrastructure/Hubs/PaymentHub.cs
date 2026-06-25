// TransactionService.Infrastructure / Hubs / PaymentHub.cs
//
// SignalR WebSocket hub — POS terminali ödeme sonucu bekler.
//
// Kullanım akışı:
//   1. POS terminali ödeme başlatır (POST /payments/confirm)
//   2. TransactionService SignalR group adını yanıtta döner
//   3. POS terminali SignalR'a bağlanır: hub.joinGroup("payment:{transactionId}")
//   4. Ödeme tamamlanınca hub üzerinden "PaymentResult" eventi gönderilir
//   5. Terminal ekranı sonucu gösterir (APPROVED / DECLINED)
//
// Group ismi: "payment:{transactionId}"
// Event: "PaymentResult" → { transactionId, status, responseCode }

using Microsoft.AspNetCore.SignalR;

namespace TransactionService.Infrastructure.Hubs;

public class PaymentHub : Hub
{
    /// <summary>
    /// Terminal veya müşteri belirli bir ödeme group'una katılır.
    /// groupName: "payment:{transactionId}"
    /// </summary>
    public async Task JoinPaymentGroup(string groupName)
        => await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

    public async Task LeavePaymentGroup(string groupName)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
}
