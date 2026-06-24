// AuthService.Domain / Entities / User.cs
//
// Kullanıcı entity'si. Sistemdeki tüm kullanıcı tiplerini (CUSTOMER, MERCHANT,
// TERMINAL, ADMIN) tek tabloda tutar; rol bazlı yetkilendirme JWT claim'leri
// üzerinden yapılır.
//
// Tasarım kararları:
//   - Şifre asla düz metin saklanmaz; BCrypt hash'i tutulur.
//   - TCKN gibi hassas kimlik bilgisi bu tabloda yok; OnboardingService'tedir.
//   - TOTP gizli anahtarı null ise TOTP aktif değil demektir.
//   - NEWSEQUENTIALID() → index fragmentation'ı minimize eder.

namespace AuthService.Domain.Entities;

public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Benzersiz kullanıcı adı. Müşteriler için GSM, merchant için email olabilir.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>BCrypt hash'i (work factor 12). Ham şifre asla saklanmaz.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Yetkilendirme rolü: CUSTOMER | MERCHANT | TERMINAL | ADMIN</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Google Authenticator için Base32 gizli anahtar. Null ise TOTP devre dışı.</summary>
    public string? TotpSecret { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // EF Core için parametre almayan constructor
    private User() { }

    public static User Create(string username, string passwordHash, string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        return new User
        {
            Username = username.ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role
        };
    }

    public void SetTotpSecret(string secret) => TotpSecret = secret;
    public void Deactivate() => IsActive = false;
}
