// AuthService.Infrastructure / Services / TotpService.cs
//
// TOTP (Time-based One-Time Password) yönetimi — Google Authenticator uyumlu.
// RFC 6238 standardını uygular; 30 saniyelik window kullanır.
//
// Kullanım senaryosu:
//   1. Kullanıcı TOTP'u ilk kez aktif etmek istediğinde:
//      → GenerateSecret() çağrılır, QR URI üretilir
//      → Kullanıcı uygulamaya tarar
//   2. Her girişte:
//      → Kullanıcının girdiği 6 haneli kod Verify() ile doğrulanır
//
// Güvenlik: Brute-force koruması API katmanında rate-limit ile sağlanır;
// burada sadece kriptografik doğrulama yapılır.

using OtpNet;

namespace AuthService.Infrastructure.Services;

public interface ITotpService
{
    (string secret, string otpAuthUri) GenerateSetup(string username, string issuer = "QrPayment");
    bool Verify(string secret, string code);
}

public class TotpService : ITotpService
{
    /// <summary>
    /// Yeni TOTP kurulumu için gizli anahtar ve QR URI üretir.
    /// Dönen secret, User.TotpSecret alanına kaydedilir.
    /// Dönen otpAuthUri, QR kod olarak kullanıcıya gösterilir.
    /// </summary>
    public (string secret, string otpAuthUri) GenerateSetup(string username, string issuer = "QrPayment")
    {
        // 20-byte (160-bit) rastgele gizli anahtar — RFC 6238 önerilenin üzerinde
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);

        // Google Authenticator'ın tarayabileceği otpauth URI formatı
        var uri = new OtpUri(OtpType.Totp, secret, username, issuer).ToString();

        return (secret, uri);
    }

    /// <summary>
    /// Kullanıcının girdiği 6 haneli kodu doğrular.
    /// Önceki ve sonraki 30 saniyelik pencereleri de kabul eder (saat farkı toleransı).
    /// </summary>
    public bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);

            // VerificationWindow(1,1): -1 ile +1 zaman adımı toleransı (±30sn)
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(1, 1));
        }
        catch
        {
            return false;
        }
    }
}
