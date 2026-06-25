// OnboardingService.Domain / Interfaces / IOnboardingRepositories.cs
//
// Onboarding servisinin repository arayüzleri.
// Domain katmanı EF Core'u tanımaz; bu arayüzler üzerinden çalışır.

using OnboardingService.Domain.Entities;

namespace OnboardingService.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Customer?> GetByGsmAsync(string gsm, CancellationToken ct = default);
    Task<Customer?> GetByIdentityHashAsync(string hash, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IMerchantRepository
{
    Task<Merchant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Merchant?> GetByTaxNumberAsync(string taxNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Merchant>> GetPendingAsync(CancellationToken ct = default);
    Task AddAsync(Merchant merchant, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITerminalRepository
{
    Task<Terminal?> GetByIdAsync(string terminalId, CancellationToken ct = default);
    Task AddAsync(Terminal terminal, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IOtpRepository
{
    /// <summary>GSM'e ait son geçerli (kullanılmamış, süresi dolmamış) OTP'yi getirir.</summary>
    Task<OtpVerification?> GetLatestValidAsync(string gsm, CancellationToken ct = default);
    Task AddAsync(OtpVerification otp, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
