// OnboardingService.Infrastructure / Persistence / Repositories / OnboardingRepositories.cs
//
// ICustomerRepository, IMerchantRepository, ITerminalRepository, IOtpRepository
// arayüzlerinin EF Core implementasyonları.

using Microsoft.EntityFrameworkCore;
using OnboardingService.Domain.Entities;
using OnboardingService.Domain.Interfaces;
using OnboardingService.Infrastructure.Persistence;

namespace OnboardingService.Infrastructure.Persistence.Repositories;

public class CustomerRepository(OnboardingDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);

    public Task<Customer?> GetByGsmAsync(string gsm, CancellationToken ct = default)
        => db.Customers.FirstOrDefaultAsync(c => c.Gsm == gsm, ct);

    public Task<Customer?> GetByIdentityHashAsync(string hash, CancellationToken ct = default)
        => db.Customers.FirstOrDefaultAsync(c => c.IdentityHash == hash, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
        => await db.Customers.AddAsync(customer, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

public class MerchantRepository(OnboardingDbContext db) : IMerchantRepository
{
    public Task<Merchant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Merchants.Include(m => m.Branches).FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<Merchant?> GetByTaxNumberAsync(string taxNumber, CancellationToken ct = default)
        => db.Merchants.FirstOrDefaultAsync(m => m.TaxNumber == taxNumber, ct);

    public async Task<IReadOnlyList<Merchant>> GetPendingAsync(CancellationToken ct = default)
        => await db.Merchants.Where(m => m.Status == "PENDING").ToListAsync(ct);

    public async Task AddAsync(Merchant merchant, CancellationToken ct = default)
        => await db.Merchants.AddAsync(merchant, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

public class TerminalRepository(OnboardingDbContext db) : ITerminalRepository
{
    public Task<Terminal?> GetByIdAsync(string terminalId, CancellationToken ct = default)
        => db.Terminals.FirstOrDefaultAsync(t => t.Id == terminalId && t.IsActive, ct);

    public async Task AddAsync(Terminal terminal, CancellationToken ct = default)
        => await db.Terminals.AddAsync(terminal, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

public class OtpRepository(OnboardingDbContext db) : IOtpRepository
{
    public Task<OtpVerification?> GetLatestValidAsync(string gsm, CancellationToken ct = default)
        => db.OtpVerifications
             .Where(o => o.Gsm == gsm && !o.IsUsed && o.ExpiresAt > DateTimeOffset.UtcNow)
             .OrderByDescending(o => o.CreatedAt)
             .FirstOrDefaultAsync(ct);

    public async Task AddAsync(OtpVerification otp, CancellationToken ct = default)
        => await db.OtpVerifications.AddAsync(otp, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
