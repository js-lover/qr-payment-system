// AuthService.Infrastructure / Persistence / Repositories / UserRepository.cs
//
// IUserRepository, IRefreshTokenRepository, ITerminalCredentialRepository
// arayüzlerinin EF Core implementasyonları.
// Domain katmanı bu sınıfları doğrudan görmez; DI üzerinden arayüzle çalışır.

using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence.Repositories;

public class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Username == username.ToLowerInvariant() && u.IsActive, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

public class RefreshTokenRepository(AuthDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => db.RefreshTokens
             .FirstOrDefaultAsync(rt => rt.Token == token && !rt.IsRevoked, ct);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default)
        => await db.RefreshTokens.AddAsync(refreshToken, ct);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Batch update — tüm aktif token'ları tek sorguda iptal eder
        await db.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.IsRevoked, true), ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

public class TerminalCredentialRepository(AuthDbContext db) : ITerminalCredentialRepository
{
    public Task<TerminalCredential?> GetByTerminalIdAsync(string terminalId, CancellationToken ct = default)
        => db.TerminalCredentials
             .FirstOrDefaultAsync(tc => tc.TerminalId == terminalId && tc.IsActive, ct);

    public async Task AddAsync(TerminalCredential credential, CancellationToken ct = default)
        => await db.TerminalCredentials.AddAsync(credential, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
