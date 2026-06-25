// OnboardingService.Api / Controllers / CustomerController.cs
//
// Müşteri kayıt ve KYC endpoint'leri.
//
// Herkese açık endpoint'ler (anonim):
//   POST /customers/register  — Yeni müşteri kaydı, SMS OTP gönderir
//   POST /customers/verify-otp — OTP doğrulama, Kafka event yayınlar
//
// Yalnızca müşteri (Customer rolü):
//   POST /customers/{id}/kyc  — KYC belgesi yükleme
//
// Yalnızca admin (Admin rolü):
//   PATCH /customers/{id}/kyc/approve  — KYC onay
//   PATCH /customers/{id}/kyc/reject   — KYC red

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnboardingService.Infrastructure.Services;
using QrPayment.Contracts.Onboarding;
using QrPayment.Shared.Models;

namespace OnboardingService.Api.Controllers;

[ApiController]
[Route("customers")]
public class CustomerController(IOnboardingService onboardingService) : ControllerBase
{
    /// <summary>Yeni müşteri kaydı. GSM'e OTP kodu SMS ile gönderilir.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<RegisterCustomerResponse>>> Register(
        [FromBody] RegisterCustomerRequest request, CancellationToken ct)
    {
        // TCKN hash'i client tarafında hesaplanıp gönderilir (Privacy by Design)
        var customerId = await onboardingService.RegisterCustomerAsync(
            request.IdentityHash,
            request.FirstName,
            request.LastName,
            request.Gsm,
            ct);

        return Ok(ApiResponse<RegisterCustomerResponse>.Ok(
            new RegisterCustomerResponse(customerId, "OTP gönderildi."),
            HttpContext.TraceIdentifier));
    }

    /// <summary>SMS ile gelen OTP kodunu doğrular. Başarıda customer.registered Kafka event'i yayınlanır.</summary>
    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<string>>> VerifyOtp(
        [FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        await onboardingService.VerifyOtpAsync(request.Gsm, request.OtpCode, ct);
        return Ok(ApiResponse<string>.Ok("OTP doğrulandı.", HttpContext.TraceIdentifier));
    }

    /// <summary>KYC belgesi yükleme. Yalnızca kimliği doğrulanmış müşteri.</summary>
    [HttpPost("{id:guid}/kyc")]
    [Authorize(Roles = "CUSTOMER")]
    public async Task<ActionResult<ApiResponse<string>>> SubmitKyc(
        Guid id, [FromBody] SubmitKycRequest request, CancellationToken ct)
    {
        await onboardingService.SubmitKycAsync(id, request.DocumentPath, ct);
        return Accepted(ApiResponse<string>.Ok("KYC belgesi alındı.", HttpContext.TraceIdentifier));
    }

    /// <summary>Admin KYC onayı. customer.kyc_approved event'i yayınlanır → WalletService cüzdanı aktive eder.</summary>
    [HttpPatch("{id:guid}/kyc/approve")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<string>>> ApproveKyc(Guid id, CancellationToken ct)
    {
        var adminId = Guid.Parse(User.FindFirst("sub")!.Value);
        await onboardingService.ApproveKycAsync(id, adminId, ct);
        return Ok(ApiResponse<string>.Ok("KYC onaylandı.", HttpContext.TraceIdentifier));
    }

    /// <summary>Admin KYC reddi. customer.kyc_rejected event'i yayınlanır.</summary>
    [HttpPatch("{id:guid}/kyc/reject")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<string>>> RejectKyc(
        Guid id, [FromBody] RejectKycRequest request, CancellationToken ct)
    {
        await onboardingService.RejectKycAsync(id, request.Reason, ct);
        return Ok(ApiResponse<string>.Ok("KYC reddedildi.", HttpContext.TraceIdentifier));
    }
}
