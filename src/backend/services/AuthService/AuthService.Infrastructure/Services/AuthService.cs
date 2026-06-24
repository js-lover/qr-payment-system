// AuthService.Infrastructure / Services / AuthService.cs
//
// Kimlik doğrulama iş mantığının merkezi. Endpoint'ler doğrudan DB ile
// konuşmaz; her şey bu servis üzerinden geçer.
//
// Sorumluluklar:
//   - Kullanıcı adı/şifre doğrulama (BCrypt)
//   - TOTP doğrulama (opsiyonel 2FA)
//   - JWT access + refresh token üretimi
//   - Refresh token rotation (eski iptal, yeni üret)
//   - Token revocation (logout)

using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using QrPayment.Shared.Exceptions;

namespace AuthService.Infrastructure.Services;

public interface IAuthenticationService
{
    Task<(string accessToken, string refreshToken)> LoginAsync(
        string username, string password, string? totpCode, CancellationToken ct = default);

    Task<(string accessToken, string refreshToken)> RefreshAsync(
        string refreshToken, CancellationToken ct = default);

    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}

public class AuthenticationService(
    IUserRepository userRepo,
    IRefreshTokenRepository refreshRepo,
    IJwtService jwtService,
    ITotpService totpService,
    IConfiguration configuration) : IAuthenticationService
{
    public async Task<(string accessToken, string refreshToken)> LoginAsync(
        string username, string password, string? totpCode, CancellationToken ct = default)
    {
        var user = await userRepo.GetByUsernameAsync(username, ct)
            ?? throw new UnauthorizedException("Kullanıcı adı veya şifre hatalı.");

        // BCrypt doğrulaması — timing-safe
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedException("Kullanıcı adı veya şifre hatalı.");

        // TOTP aktif ise doğrula
        if (user.TotpSecret is not null)
        {
            if (string.IsNullOrWhiteSpace(totpCode))
                throw new UnauthorizedException("İki faktörlü doğrulama kodu gerekli.");

            if (!totpService.Verify(user.TotpSecret, totpCode))
                throw new UnauthorizedException("İki faktörlü doğrulama kodu hatalı.");
        }

        return await IssueTokensAsync(user, ct);
    }

    public async Task<(string accessToken, string refreshToken)> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var existing = await refreshRepo.GetByTokenAsync(refreshToken, ct)
            ?? throw new UnauthorizedException("Refresh token geçersiz veya süresi dolmuş.");

        if (!existing.IsValid)
            throw new UnauthorizedException("Refresh token süresi dolmuş.");

        var user = await userRepo.GetByIdAsync(existing.UserId, ct)
            ?? throw new UnauthorizedException("Kullanıcı bulunamadı.");

        // Rotation: eski token'ı iptal et, yeni çift üret
        existing.Revoke();
        await refreshRepo.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var existing = await refreshRepo.GetByTokenAsync(refreshToken, ct);
        if (existing is null) return; // Zaten geçersiz — idempotent

        existing.Revoke();
        await refreshRepo.SaveChangesAsync(ct);
    }

    private async Task<(string accessToken, string refreshToken)> IssueTokensAsync(
        User user, CancellationToken ct)
    {
        var accessToken = jwtService.GenerateAccessToken(user.Id, user.Role, user.Username);
        var rawRefreshToken = jwtService.GenerateRefreshToken();

        var expiryDays = int.Parse(configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");
        var refreshTokenEntity = RefreshToken.Create(
            user.Id, rawRefreshToken, DateTimeOffset.UtcNow.AddDays(expiryDays));

        await refreshRepo.AddAsync(refreshTokenEntity, ct);
        await refreshRepo.SaveChangesAsync(ct);

        return (accessToken, rawRefreshToken);
    }
}
