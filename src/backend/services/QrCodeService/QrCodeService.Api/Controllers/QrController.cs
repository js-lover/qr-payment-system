// QrCodeService.Api / Controllers / QrController.cs
//
// QR kod üretim ve doğrulama endpoint'leri.
//
// POS Terminal (TERMINAL rolü):
//   POST /qr/generate    — Ödeme QR kodu üretir, terminalin ekranına çizdirilir
//   GET  /qr/{token}/status — QR durumu (PENDING/CLAIMED/EXPIRED)
//
// Mobil Uygulama (CUSTOMER rolü):
//   GET  /qr/{token}/validate — Müşteri QR'ı taradıktan sonra ödeme bilgilerini gösterir

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrCodeService.Infrastructure.Services;
using QrPayment.Contracts.QrCode;
using QrPayment.Shared.Exceptions;
using QrPayment.Shared.Models;

namespace QrCodeService.Api.Controllers;

[ApiController]
[Route("qr")]
[Authorize]
public class QrController(IQrTokenService qrTokenService) : ControllerBase
{
    /// <summary>
    /// POS terminal'i için QR ödeme kodu üretir.
    /// Yanıttaki QrContent değeri terminal ekranında QR kod olarak render edilir.
    /// Token 90 saniye geçerlidir.
    /// </summary>
    [HttpPost("generate")]
    [Authorize(Roles = "TERMINAL,MERCHANT,ADMIN")]
    public async Task<ActionResult<ApiResponse<GenerateQrResponse>>> Generate(
        [FromBody] GenerateQrRequest request, CancellationToken ct)
    {
        var qrToken = await qrTokenService.GenerateAsync(
            request.TerminalId,
            request.MerchantId,
            request.MerchantTitle,
            request.Amount,
            ct);

        var response = new GenerateQrResponse(
            qrToken.Token,
            qrToken.QrContent,
            qrToken.ExpiresAt,
            qrToken.RemainingSeconds,
            qrToken.Amount,
            qrToken.MerchantTitle);

        return Ok(ApiResponse<GenerateQrResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Müşteri mobil uygulamasında QR taradıktan sonra ödeme bilgilerini gösterir.
    /// Token geçerliyse işyeri adı, tutar ve kalan süre döner.
    /// Müşteri bu ekranda ödemeyi onaylar.
    /// </summary>
    [HttpGet("{token}/validate")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<ApiResponse<ValidateQrResponse>>> Validate(
        string token, CancellationToken ct)
    {
        var qrToken = await qrTokenService.ValidateAsync(token, ct)
            ?? throw new NotFoundException("QrToken", token);

        var response = new ValidateQrResponse(
            qrToken.Token,
            qrToken.TerminalId,
            qrToken.MerchantId,
            qrToken.MerchantTitle,
            qrToken.Amount,
            qrToken.Status,
            qrToken.ExpiresAt,
            qrToken.RemainingSeconds);

        return Ok(ApiResponse<ValidateQrResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// QR token'ı atomik olarak claim eder. TransactionService tarafından çağrılır.
    /// Aynı QR iki kez claim edilemez — race condition koruması Redis SETNX ile sağlanır.
    /// </summary>
    [HttpPost("{token}/claim")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<ApiResponse<bool>>> Claim(string token, CancellationToken ct)
    {
        var claimed = await qrTokenService.ClaimAsync(token, ct);
        if (!claimed)
            throw new BusinessRuleException("QR_ALREADY_CLAIMED", "QR Zaten Kullanıldı",
                "Bu QR kodu zaten başka bir ödeme için kullanıldı.", 409);

        return Ok(ApiResponse<bool>.Ok(true, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// QR token durumunu döner. POS terminal polling için kullanır.
    /// PENDING → müşteri taramadı
    /// CLAIMED → ödeme başlatıldı
    /// Null döndüğünde token süresi dolmuş (EXPIRED).
    /// </summary>
    [HttpGet("{token}/status")]
    [Authorize(Roles = "TERMINAL,MERCHANT,ADMIN")]
    public async Task<ActionResult<ApiResponse<QrStatusResponse>>> GetStatus(
        string token, CancellationToken ct)
    {
        var qrToken = await qrTokenService.GetAsync(token, ct);

        if (qrToken is null)
            return Ok(ApiResponse<QrStatusResponse>.Ok(
                new QrStatusResponse(token, "EXPIRED", 0), HttpContext.TraceIdentifier));

        return Ok(ApiResponse<QrStatusResponse>.Ok(
            new QrStatusResponse(token, qrToken.Status, qrToken.RemainingSeconds),
            HttpContext.TraceIdentifier));
    }
}
