// TransactionService.Api / Controllers / PaymentController.cs
//
// Ödeme onay ve durum endpoint'leri.
//
// Müşteri (CUSTOMER rolü):
//   POST /payments/confirm  — QR ödemeyi başlat (orchestration akışı burada)
//   GET  /payments/{id}     — İşlem durumunu sorgula

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrPayment.Contracts.Transaction;
using QrPayment.Shared.Models;
using TransactionService.Infrastructure.Services;

namespace TransactionService.Api.Controllers;

[ApiController]
[Route("payments")]
[Authorize]
public class PaymentController(IPaymentService paymentService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

    /// <summary>
    /// QR ödeme akışını başlatır.
    /// 1. QR token doğrula → claim et
    /// 2. Wallet provision
    /// 3. ISO 8583 Bank Simulator
    /// 4. Wallet confirm/release
    /// 5. Kafka event
    /// 6. SignalR bildirim
    /// POS terminali sonucu SignalR üzerinden alır (signalREndpoint + signalRGroup).
    /// </summary>
    [HttpPost("confirm")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<ApiResponse<PaymentConfirmResponse>>> Confirm(
        [FromBody] PaymentConfirmRequest request, CancellationToken ct)
    {
        var transaction = await paymentService.InitiatePaymentAsync(
            request.QrToken, CurrentUserId, ct);

        var response = new PaymentConfirmResponse(
            transaction.Id,
            transaction.Status,
            SignalREndpoint: "/hubs/payment",
            SignalRGroup: $"payment:{transaction.Id}");

        return Ok(ApiResponse<PaymentConfirmResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>İşlem durumunu sorgular. Polling veya push bildirim sonrası durum kontrolü.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PaymentStatusResponse>>> GetStatus(
        Guid id, CancellationToken ct)
    {
        var transaction = await paymentService.GetStatusAsync(id, ct);

        var response = new PaymentStatusResponse(
            transaction.Id,
            transaction.Status,
            transaction.IsoResponseCode,
            transaction.Stan,
            transaction.Rrn,
            transaction.CompletedAt);

        return Ok(ApiResponse<PaymentStatusResponse>.Ok(response, HttpContext.TraceIdentifier));
    }
}
