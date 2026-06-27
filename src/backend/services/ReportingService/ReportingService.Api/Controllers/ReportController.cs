// ReportingService.Api / Controllers / ReportController.cs
//
// Raporlama ve analitik endpoint'leri.
//
// Müşteri (CUSTOMER rolü):
//   GET /reports/my-transactions    — Kişisel işlem geçmişi (Elasticsearch sorgusu)
//
// Admin / Merchant (ADMIN, MERCHANT rolü):
//   GET /reports/transactions       — Filtreli işlem araması (tarih, merchant, müşteri)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrPayment.Shared.Models;
using ReportingService.Domain.Models;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Api.Controllers;

[ApiController]
[Route("reports")]
[Authorize]
public class ReportController(IElasticsearchService esService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

    /// <summary>
    /// Müşterinin kendi işlem geçmişi.
    /// JWT token'daki sub claim ile filtreleme yapılır (başka kullanıcı erişemez).
    /// </summary>
    [HttpGet("my-transactions")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransactionDocument>>>> GetMyTransactions(
        [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var transactions = await esService.GetByCustomerAsync(CurrentUserId, page, size, ct);
        return Ok(ApiResponse<IReadOnlyList<TransactionDocument>>.Ok(transactions, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Gelişmiş işlem arama. Admin ve merchant paneli için.
    /// Tarih aralığı, müşteri ID ve merchant ID ile filtreleme desteklenir.
    /// </summary>
    [HttpGet("transactions")]
    [Authorize(Roles = "ADMIN,MERCHANT")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransactionDocument>>>> SearchTransactions(
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? merchantId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        // merchantId filtresi query param ile dışarıdan verilebilir.
        // JWT sub ≠ merchant entity ID olduğundan otomatik atama yapılmıyor.
        var transactions = await esService.SearchAsync(customerId, merchantId, from, to, page, size, ct);
        return Ok(ApiResponse<IReadOnlyList<TransactionDocument>>.Ok(transactions, HttpContext.TraceIdentifier));
    }
}
