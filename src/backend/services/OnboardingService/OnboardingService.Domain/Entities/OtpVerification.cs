// OnboardingService.Domain / Entities / OtpVerification.cs
//
// SMS OTP doğrulama kaydı.
//
// Akış:
//   1. Müşteri telefon numarasını girer → OtpVerification.Create() → SMS gönderilir
//   2. Müşteri 6 haneli kodu girer → Verify() ile doğrulanır
//   3. Doğrulama başarılıysa IsUsed = true yapılır (tekrar kullanım önlenir)
//
// Güvenlik kuralları:
//   - OTP 3 dakika geçerli (ExpiresAt)
//   - Maksimum 3 yanlış deneme (AttemptCount ≥ 3 → kod kilitlenir)
//   - Kullanılmış kod tekrar kabul edilmez (IsUsed = true)
//   - Brute-force ek koruma: API rate-limit (GSM + IP bazlı)

namespace OnboardingService.Domain.Entities;

public class OtpVerification
{
    public long Id { get; private set; }
    public string Gsm { get; private set; } = string.Empty;

    /// <summary>6 haneli sayısal OTP kodu.</summary>
    public string OtpCode { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }

    /// <summary>Kaç kez yanlış kod girildi. 3'te kilitlenir.</summary>
    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private OtpVerification() { }

    public static OtpVerification Create(string gsm, string code, int expiryMinutes = 3)
        => new()
        {
            Gsm = gsm,
            OtpCode = code,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
        };

    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
    public bool IsLocked => AttemptCount >= 3;

    /// <summary>
    /// Kodu doğrula. Doğruysa IsUsed = true set eder.
    /// Yanlışsa AttemptCount artar.
    /// </summary>
    public bool Verify(string inputCode)
    {
        if (IsUsed || IsExpired || IsLocked) return false;

        if (OtpCode != inputCode.Trim())
        {
            AttemptCount++;
            return false;
        }

        IsUsed = true;
        return true;
    }
}
