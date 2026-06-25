// OnboardingService.Domain / Entities / Customer.cs
//
// Müşteri entity'si. Kişisel kimlik doğrulama ve KYC sürecini yönetir.
//
// Güvenlik kararları:
//   - TCKN (Türkiye Cumhuriyeti Kimlik Numarası) asla düz metin saklanmaz.
//     SHA-256 hash'i tutulur; sorgulama sırasında girdi hash'lenerek karşılaştırılır.
//   - GSM numarası SMS OTP doğrulama ve iletişim için kullanılır; benzersiz olmalı.
//   - KYC durumu: PENDING → (APPROVED | REJECTED)
//
// KYC onaylandığında Kafka'ya customer.kyc_approved eventi yayınlanır;
// WalletService bu event'i dinleyerek müşterinin cüzdanını aktive eder.

namespace OnboardingService.Domain.Entities;

public class Customer
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// TCKN'nin SHA-256 hash'i. Ham TCKN asla saklanmaz.
    /// Duplikasyon kontrolü için benzersiz olmalı.
    /// </summary>
    public string IdentityHash { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;

    /// <summary>+905551234567 formatında GSM numarası. SMS OTP için kullanılır.</summary>
    public string Gsm { get; private set; } = string.Empty;

    /// <summary>KYC durumu: PENDING | APPROVED | REJECTED</summary>
    public string KycStatus { get; private set; } = "PENDING";

    public DateTimeOffset? KycVerifiedAt { get; private set; }

    /// <summary>FCM push notification için cihaz token'ı. Opsiyonel.</summary>
    public string? FcmToken { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Customer() { }

    public static Customer Create(string identityHash, string firstName, string lastName, string gsm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(gsm);

        return new Customer
        {
            IdentityHash = identityHash,
            FirstName = firstName,
            LastName = lastName,
            Gsm = gsm
        };
    }

    /// <summary>
    /// Admin KYC başvurusunu onayladığında çağrılır.
    /// Sonrasında domain event yayınlanmalı.
    /// </summary>
    public void ApproveKyc()
    {
        KycStatus = "APPROVED";
        KycVerifiedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>KYC reddedildiğinde çağrılır. Müşteri yeniden başvurabilir.</summary>
    public void RejectKyc(string reason)
    {
        KycStatus = "REJECTED";
        // İleride rejection reason saklanabilir
    }

    public void SetFcmToken(string token) => FcmToken = token;
    public void Deactivate() => IsActive = false;
}
