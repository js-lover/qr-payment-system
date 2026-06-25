// QrPayment.Contracts / Onboarding / OnboardingContracts.cs
//
// Onboarding Service API için request/response DTO'ları.
// Hem API hem de client projeler bu DTO'ları kullanır.
//
// Içindekiler:
//   - RegisterCustomerRequest/Response  — Müşteri kaydı ve OTP gönderimi
//   - VerifyOtpRequest                  — SMS OTP doğrulama
//   - SubmitKycRequest                  — KYC belge yolu
//   - RejectKycRequest                  — Admin KYC red nedeni
//   - ApplyMerchantRequest/Response     — İşyeri başvurusu
//   - CreateTerminalRequest/Response    — Terminal oluşturma

namespace QrPayment.Contracts.Onboarding;

// ─── Müşteri ────────────────────────────────────────────────────────────────

/// <summary>
/// Müşteri kaydı isteği.
/// IdentityHash: TCKN'nin SHA-256 hash'i (client tarafında hesaplanır).
/// TCKN asla düz metin API'ye gönderilmez.
/// </summary>
public record RegisterCustomerRequest(
    string IdentityHash,
    string FirstName,
    string LastName,
    string Gsm);

public record RegisterCustomerResponse(Guid CustomerId, string Message);

public record VerifyOtpRequest(string Gsm, string OtpCode);

/// <summary>KYC belge yolu. MVP'de yerel dosya, prod'da blob storage URL'si olur.</summary>
public record SubmitKycRequest(string DocumentPath);

public record RejectKycRequest(string Reason);

// ─── İşyeri ──────────────────────────────────────────────────────────────────

/// <summary>
/// İşyeri başvurusu.
/// Mcc: ISO 18245 standartı. Örn: "5411" (bakkal), "5812" (restoran).
/// </summary>
public record ApplyMerchantRequest(
    string Title,
    string TaxNumber,
    string Iban,
    string Mcc);

public record ApplyMerchantResponse(Guid MerchantId, string Message);

public record CreateTerminalRequest(Guid? BranchId = null);

/// <summary>Terminal yanıtı. TerminalId POS cihazına yüklenir.</summary>
public record CreateTerminalResponse(string TerminalId, string Message);
