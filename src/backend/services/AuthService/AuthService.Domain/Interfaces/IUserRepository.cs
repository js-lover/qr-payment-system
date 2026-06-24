// AuthService.Domain / Interfaces / IUserRepository.cs
//
// Repository arayüzleri. Domain katmanı sadece bu arayüzleri bilir;
// implementasyon Infrastructure katmanındadır (EF Core).
// Bu sayede domain testi sırasında mock repository kullanılabilir.

using AuthService.Domain.Entities;

namespace AuthService.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);

    /// <summary>Kullanıcının tüm aktif refresh token'larını iptal eder (tam logout).</summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITerminalCredentialRepository
{
    Task<TerminalCredential?> GetByTerminalIdAsync(string terminalId, CancellationToken ct = default);
    Task AddAsync(TerminalCredential credential, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
