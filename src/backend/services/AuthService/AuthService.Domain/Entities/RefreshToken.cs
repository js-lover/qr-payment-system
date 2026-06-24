// AuthService.Domain / Entities / RefreshToken.cs
//
// Refresh token entity'si. Her kullanıcının birden fazla aktif refresh token'ı
// olabilir (farklı cihazlardan giriş senaryosu).
//
// Güvenlik notu: Refresh token rotation uygulanır.
// /auth/refresh çağrıldığında:
//   1. Mevcut refresh token iptal edilir (IsRevoked = true)
//   2. Yeni bir refresh token üretilip kullanıcıya döndürülür
// Bu sayede çalınmış token'lar bir kez kullanıldığında sistem anlar.

namespace AuthService.Domain.Entities;

public class RefreshToken
{
    public long Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Cryptographically random 64-byte token (Base64 encoded).</summary>
    public string Token { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string token, DateTimeOffset expiresAt)
    {
        return new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
    public bool IsValid => !IsRevoked && !IsExpired;

    /// <summary>Token'ı geçersiz kılar. Logout veya rotation sırasında çağrılır.</summary>
    public void Revoke() => IsRevoked = true;
}
