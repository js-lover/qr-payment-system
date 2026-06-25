// WalletService.Api / Controllers / WalletController.cs
//
// Cüzdan endpoint'leri.
//
// Müşteri (CUSTOMER rolü):
//   GET  /wallet/balance        — Bakiye bilgisi
//   POST /wallet/topup          — Para yükle
//
// Dahili servisler (servis-arası iletişim, JWT içindeki sub ile ownerId çözülür):
//   POST /wallet/provision       — QR ödeme için tutar blokaj (TransactionService çağırır)
//   POST /wallet/confirm         — Başarılı ödeme sonrası blokajı düşür
//   POST /wallet/release         — Başarısız/iptal ödeme sonrası blokajı serbest bırak

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrPayment.Contracts.Wallet;
using QrPayment.Shared.Models;
using WalletService.Infrastructure.Services;

namespace WalletService.Api.Controllers;

[ApiController]
[Route("wallet")]
[Authorize]
public class WalletController(IWalletService walletService) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirst("sub")!.Value);

    /// <summary>Mevcut kullanıcının cüzdan bakiyesini döner.</summary>
    [HttpGet("balance")]
    public async Task<ActionResult<ApiResponse<BalanceResponse>>> GetBalance(CancellationToken ct)
    {
        var wallet = await walletService.GetBalanceAsync(CurrentUserId, ct);

        var response = new BalanceResponse(
            wallet.AvailableBalance / 100m,
            wallet.BlockedBalance / 100m,
            wallet.TotalBalance / 100m,
            wallet.Currency,
            DateTimeOffset.UtcNow);

        return Ok(ApiResponse<BalanceResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Cüzdana para yükler.
    /// Request.Amount TL cinsindendir; servis içinde kuruşa çevrilir (×100).
    /// </summary>
    [HttpPost("topup")]
    public async Task<ActionResult<ApiResponse<string>>> Topup(
        [FromBody] TopupRequest request, CancellationToken ct)
    {
        var amountKurus = (long)(request.Amount * 100);
        await walletService.TopupAsync(CurrentUserId, amountKurus, request.ReferenceId, ct);
        return Ok(ApiResponse<string>.Ok("Bakiye güncellendi.", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// QR ödeme için tutar rezerve eder (blokaj).
    /// TransactionService bu endpoint'i dahili olarak çağırır.
    /// </summary>
    [HttpPost("provision")]
    public async Task<ActionResult<ApiResponse<string>>> Provision(
        [FromBody] ProvisionRequest request, CancellationToken ct)
    {
        var amountKurus = (long)(request.Amount * 100);
        await walletService.ProvisionAsync(CurrentUserId, amountKurus, request.QrToken, ct);
        return Ok(ApiResponse<string>.Ok("Provizyon oluşturuldu.", HttpContext.TraceIdentifier));
    }

    /// <summary>Başarılı ödeme sonrası blokajı düşürür.</summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<ApiResponse<string>>> Confirm(
        [FromBody] ConfirmProvisionRequest request, CancellationToken ct)
    {
        var amountKurus = (long)(request.Amount * 100);
        await walletService.ConfirmProvisionAsync(CurrentUserId, amountKurus, request.TransactionId, ct);
        return Ok(ApiResponse<string>.Ok("Ödeme onaylandı.", HttpContext.TraceIdentifier));
    }

    /// <summary>Başarısız/iptal ödeme sonrası blokajı serbest bırakır.</summary>
    [HttpPost("release")]
    public async Task<ActionResult<ApiResponse<string>>> Release(
        [FromBody] ReleaseProvisionRequest request, CancellationToken ct)
    {
        var amountKurus = (long)(request.Amount * 100);
        await walletService.ReleaseProvisionAsync(CurrentUserId, amountKurus, request.QrToken, ct);
        return Ok(ApiResponse<string>.Ok("Provizyon serbest bırakıldı.", HttpContext.TraceIdentifier));
    }
}
