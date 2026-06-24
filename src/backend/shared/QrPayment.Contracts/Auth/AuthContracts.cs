// QrPayment.Contracts / Auth / AuthContracts.cs
//
// AuthService HTTP endpoint'lerinin istek ve yanıt DTO'ları.
// İstemciler (mobile, web, terminal) bu tipleri kullanarak
// AuthService ile konuşur.
//
// JWT akışı:
//   TokenRequest → /auth/token → TokenResponse (accessToken + refreshToken)
//   RefreshRequest → /auth/refresh → TokenResponse (yeni accessToken)
//   RevokeRequest → /auth/revoke → 204 No Content

namespace QrPayment.Contracts.Auth;

// ─── Token ───────────────────────────────────────────────────────────────────

/// <summary>Kullanıcı adı + şifre ile JWT token alma isteği.</summary>
public record TokenRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>TOTP etkin hesaplar için Google Authenticator kodu (6 hane).</summary>
    public string? TotpCode { get; init; }
}

/// <summary>Başarılı kimlik doğrulamasının yanıtı. Access + refresh token çifti döner.</summary>
public record TokenResponse
{
    /// <summary>Kısa ömürlü JWT (15 dk). Her API isteğine Authorization: Bearer ile eklenir.</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Uzun ömürlü token (7 gün). Sadece /auth/refresh endpoint'inde kullanılır.</summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>AccessToken geçerlilik süresi (saniye cinsinden). İstemci için bilgi amaçlı.</summary>
    public int ExpiresIn { get; init; } = 900;

    public string TokenType { get; init; } = "Bearer";
}

/// <summary>Refresh token ile yeni access token alma isteği.</summary>
public record RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>Refresh token'ı iptal etme isteği (logout).</summary>
public record RevokeRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

// ─── TOTP ────────────────────────────────────────────────────────────────────

/// <summary>TOTP (Google Authenticator) kurulum başlatma yanıtı.</summary>
public record TotpSetupResponse
{
    /// <summary>QR kod üretmek için kullanılan otpauth:// URI.</summary>
    public string OtpAuthUri { get; init; } = string.Empty;

    /// <summary>Manuel giriş için gizli anahtar (Base32).</summary>
    public string SecretKey { get; init; } = string.Empty;
}

/// <summary>TOTP kodu doğrulama isteği.</summary>
public record TotpVerifyRequest
{
    /// <summary>Authenticator uygulamasından alınan 6 haneli kod.</summary>
    public string Code { get; init; } = string.Empty;
}

// ─── Terminal ────────────────────────────────────────────────────────────────

/// <summary>
/// Terminal için mTLS + HMAC challenge isteği.
/// Sunucu bir challenge token döner; terminal bunu HMAC-SHA256 ile imzalayıp
/// /auth/terminal/verify'a gönderir.
/// </summary>
public record TerminalChallengeRequest
{
    public string TerminalId { get; init; } = string.Empty;

    /// <summary>Replay attack önlemek için 32 karakterlik rastgele değer.</summary>
    public string Nonce { get; init; } = string.Empty;
}

/// <summary>Terminal challenge yanıtı.</summary>
public record TerminalChallengeResponse
{
    /// <summary>Terminal'in imzalaması gereken challenge token.</summary>
    public string ChallengeToken { get; init; } = string.Empty;

    /// <summary>Challenge'ın geçerlilik bitiş zamanı (5 dakika).</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
