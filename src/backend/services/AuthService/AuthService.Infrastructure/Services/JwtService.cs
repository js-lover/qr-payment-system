// AuthService.Infrastructure / Services / JwtService.cs
//
// JWT token üretimi ve doğrulaması.
//
// Algoritma: RS256 (RSA + SHA-256) — asimetrik anahtar çifti kullanılır.
//   Private key → token imzalamak için (sadece AuthService bilir)
//   Public key  → token doğrulamak için (tüm servisler ve Kong kullanabilir)
//
// HS256 yerine RS256 tercih edilme nedeni:
//   HS256'da tüm servisler aynı gizli anahtarı paylaşır; biri ele geçirilirse
//   tüm sistem tehlikeye girer. RS256'da sadece public key paylaşılır.
//
// Access token claim'leri:
//   - sub     : kullanıcı UUID'si
//   - role    : CUSTOMER | MERCHANT | TERMINAL | ADMIN
//   - jti     : her token için benzersiz ID (opsiyonel revocation için)
//
// Anahtar üretimi (ilk kurulumda bir kez):
//   openssl genrsa -out jwt-private.pem 2048
//   openssl rsa -in jwt-private.pem -pubout -out jwt-public.pem

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Services;

public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string role, string username);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}

public class JwtService(IConfiguration configuration) : IJwtService
{
    // Lazy initialization — dosya her istekte tekrar okunmaz
    private RsaSecurityKey? _privateKey;
    private RsaSecurityKey? _publicKey;

    private RsaSecurityKey GetPrivateKey()
    {
        if (_privateKey is not null) return _privateKey;

        var path = configuration["Jwt:PrivateKeyPath"]
                   ?? throw new InvalidOperationException("Jwt:PrivateKeyPath is not configured.");
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        _privateKey = new RsaSecurityKey(rsa);
        return _privateKey;
    }

    private RsaSecurityKey GetPublicKey()
    {
        if (_publicKey is not null) return _publicKey;

        var path = configuration["Jwt:PublicKeyPath"]
                   ?? throw new InvalidOperationException("Jwt:PublicKeyPath is not configured.");
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        _publicKey = new RsaSecurityKey(rsa);
        return _publicKey;
    }

    public string GenerateAccessToken(Guid userId, string role, string username)
    {
        var expiryMinutes = int.Parse(configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var credentials = new SigningCredentials(GetPrivateKey(), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "qrpay-auth",
            audience: configuration["Jwt:Audience"] ?? "qrpay-services",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Cryptographically random 64-byte refresh token üretir.</summary>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Token'ı doğrular; geçerli ise ClaimsPrincipal döner, geçersiz ise null.
    /// /auth/refresh endpoint'inde kullanılır.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "qrpay-auth",
                ValidateAudience = true,
                ValidAudience = configuration["Jwt:Audience"] ?? "qrpay-services",
                ValidateLifetime = true,
                IssuerSigningKey = GetPublicKey(),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30) // Küçük saat farkı toleransı
            };

            return handler.ValidateToken(token, validationParams, out _);
        }
        catch
        {
            return null;
        }
    }
}
