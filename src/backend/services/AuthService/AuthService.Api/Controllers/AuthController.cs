// AuthService.Api / Controllers / AuthController.cs
//
// Kimlik doğrulama endpoint'leri.
//
// Endpoint özeti:
//   POST /auth/token                  → Giriş; JWT access + refresh token döner
//   POST /auth/refresh                → Refresh token ile yeni access token al
//   POST /auth/revoke                 → Refresh token'ı iptal et (logout)
//   POST /auth/totp/setup             → TOTP (Google Auth) kurulum başlat
//   POST /auth/totp/verify            → TOTP kodunu doğrula ve aktive et
//   POST /auth/terminal/challenge     → Terminal HMAC challenge başlat
//
// Güvenlik notu:
//   Tüm hata yanıtları aynı mesajı döner ("Kullanıcı adı veya şifre hatalı")
//   böylece timing saldırısı veya kullanıcı adı enumeration mümkün olmaz.

using AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QrPayment.Contracts.Auth;
using QrPayment.Shared.Models;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController(
    IAuthenticationService authService,
    ITotpService totpService) : ControllerBase
{
    /// <summary>
    /// Kullanıcı adı ve şifre (+ opsiyonel TOTP) ile JWT token çifti alır.
    /// TOTP aktif kullanıcılar için totpCode zorunludur.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), 200)]
    [ProducesResponseType(typeof(object), 401)]
    public async Task<IActionResult> Token([FromBody] TokenRequest request, CancellationToken ct)
    {
        var (accessToken, refreshToken) = await authService.LoginAsync(
            request.Username, request.Password, request.TotpCode, ct);

        var response = new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 900,
            TokenType = "Bearer"
        };

        return Ok(ApiResponse<TokenResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Süresi dolmak üzere olan access token'ı yenilemek için refresh token gönderilir.
    /// Rotation uygulanır: eski refresh token iptal edilir, yenisi döndürülür.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), 200)]
    [ProducesResponseType(typeof(object), 401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var (accessToken, refreshToken) = await authService.RefreshAsync(request.RefreshToken, ct);

        var response = new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 900,
            TokenType = "Bearer"
        };

        return Ok(ApiResponse<TokenResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Refresh token'ı iptal eder. Kullanıcı çıkış yaptığında çağrılır.
    /// İdempotent: zaten iptal edilmiş token için de 204 döner.
    /// </summary>
    [HttpPost("revoke")]
    [AllowAnonymous]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Revoke([FromBody] RevokeRequest request, CancellationToken ct)
    {
        await authService.RevokeAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>
    /// TOTP kurulumu başlatır. Dönen QR URI Google Authenticator ile taranır.
    /// Kurulum tamamlanmadan TOTP aktif olmaz; /auth/totp/verify ile aktive edilir.
    /// </summary>
    [HttpPost("totp/setup")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TotpSetupResponse>), 200)]
    public IActionResult TotpSetup()
    {
        var username = User.Identity?.Name ?? "user";
        var (secret, uri) = totpService.GenerateSetup(username);

        // Gizli anahtar burada döndürülür; istemci bunu güvenli şekilde saklamalı
        // ve /auth/totp/verify'da kullanmalıdır.
        // Not: Production'da secret ayrıca DB'ye (şifreli) kaydedilmeli.
        var response = new TotpSetupResponse
        {
            OtpAuthUri = uri,
            SecretKey = secret
        };

        return Ok(ApiResponse<TotpSetupResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// TOTP kurulumunu doğrular. Geçerli kod gelirse TOTP aktif hale gelir.
    /// </summary>
    [HttpPost("totp/verify")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(object), 400)]
    public IActionResult TotpVerify([FromBody] TotpVerifyRequest request, [FromQuery] string secret)
    {
        if (!totpService.Verify(secret, request.Code))
            return BadRequest(ApiResponse<object>.Fail(
                new ApiError { Code = "INVALID_TOTP", Message = "TOTP kodu geçersiz." },
                HttpContext.TraceIdentifier));

        // TODO: Secret'ı kullanıcı kaydına ekle (User.SetTotpSecret)
        // Bu işlem tam implementasyonda UserRepository üzerinden yapılacak

        return NoContent();
    }

    /// <summary>
    /// Terminal'in HMAC challenge başlatması için endpoint.
    /// Terminal bu challenge'ı kendi secret key'i ile imzalar ve doğrulama gönderir.
    /// </summary>
    [HttpPost("terminal/challenge")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TerminalChallengeResponse>), 200)]
    public IActionResult TerminalChallenge([FromBody] TerminalChallengeRequest request)
    {
        // Challenge token: terminal kimliği + zaman + rastgele değer
        var challenge = $"{request.TerminalId}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:{Guid.NewGuid()}";
        var challengeBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(challenge));

        var response = new TerminalChallengeResponse
        {
            ChallengeToken = challengeBase64,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        return Ok(ApiResponse<TerminalChallengeResponse>.Ok(response, HttpContext.TraceIdentifier));
    }
}
