// OnboardingService.Api / Controllers / MerchantController.cs
//
// İşyeri ve terminal başvuru endpoint'leri.
//
// Herkese açık:
//   POST /merchants/apply      — İşyeri başvurusu (PENDING durumuyla kaydedilir)
//
// Admin rolü:
//   PATCH /merchants/{id}/approve  — İşyeri onayı → merchant.approved Kafka event
//   POST  /merchants/{id}/terminals — Terminal oluştur → terminal.created Kafka event

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnboardingService.Infrastructure.Services;
using QrPayment.Contracts.Onboarding;
using QrPayment.Shared.Models;

namespace OnboardingService.Api.Controllers;

[ApiController]
[Route("merchants")]
public class MerchantController(IOnboardingService onboardingService) : ControllerBase
{
    /// <summary>Yeni işyeri başvurusu. PENDING durumunda kaydedilir, admin onayı gerektirir.</summary>
    [HttpPost("apply")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ApplyMerchantResponse>>> Apply(
        [FromBody] ApplyMerchantRequest request, CancellationToken ct)
    {
        var merchantId = await onboardingService.ApplyMerchantAsync(
            request.Title,
            request.TaxNumber,
            request.Iban,
            request.Mcc,
            ct);

        return Ok(ApiResponse<ApplyMerchantResponse>.Ok(
            new ApplyMerchantResponse(merchantId, "Başvurunuz alındı, inceleme sürecindedir."),
            HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Admin işyerini onaylar.
    /// merchant.approved Kafka event'i yayınlanır → AuthService merchant kullanıcısı açar.
    /// </summary>
    [HttpPatch("{id:guid}/approve")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<string>>> Approve(Guid id, CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirst("sub")!.Value);
        await onboardingService.ApproveMerchantAsync(id, adminId, ct);
        return Ok(ApiResponse<string>.Ok("İşyeri onaylandı.", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// İşyerine terminal oluşturur ve HMAC secret key üretir.
    /// terminal.created Kafka event'i yayınlanır → AuthService terminal credential kaydeder.
    /// Yanıtta dönen secretKey terminal'e kurulumu sırasında bir kez gösterilir, sonra saklanmaz.
    /// </summary>
    [HttpPost("{id:guid}/terminals")]
    [Authorize(Roles = "ADMIN,MERCHANT")]
    public async Task<ActionResult<ApiResponse<CreateTerminalResponse>>> CreateTerminal(
        Guid id, [FromBody] CreateTerminalRequest request, CancellationToken ct)
    {
        var terminalId = await onboardingService.CreateTerminalAsync(id, request.BranchId, ct);
        return Ok(ApiResponse<CreateTerminalResponse>.Ok(
            new CreateTerminalResponse(terminalId, "Terminal oluşturuldu."),
            HttpContext.TraceIdentifier));
    }
}
