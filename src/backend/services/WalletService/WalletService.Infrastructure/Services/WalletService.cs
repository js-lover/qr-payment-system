// WalletService.Infrastructure / Services / WalletService.cs
//
// Cüzdan iş mantığı — bakiye, topup, provizyon, onay/iptal.
//
// Tüm bakiye değişiklikleri UPDLOCK + ROWLOCK ile kilitlenir (race condition koruması).
// Her değişiklik WalletLedger tablosuna immutable kayıt olarak eklenir (audit trail).
//
// Kafka consumer entegrasyonu:
//   CustomerKycApprovedConsumer → WalletService.ActivateWalletAsync() çağırır
//   Böylece cüzdan KYC onaylandıktan sonra otomatik aktive olur.

using Microsoft.EntityFrameworkCore;
using WalletService.Domain.Entities;
using WalletService.Domain.Interfaces;
using WalletService.Infrastructure.Persistence;
using QrPayment.Shared.Exceptions;

namespace WalletService.Infrastructure.Services;

public interface IWalletService
{
    Task<Wallet> CreateWalletAsync(Guid ownerId, string ownerType = "CUSTOMER", CancellationToken ct = default);
    Task ActivateWalletAsync(Guid ownerId, CancellationToken ct = default);
    Task<Wallet> GetBalanceAsync(Guid ownerId, CancellationToken ct = default);
    Task TopupAsync(Guid ownerId, long amountKurus, string referenceId, CancellationToken ct = default);
    Task ProvisionAsync(Guid ownerId, long amountKurus, string qrToken, CancellationToken ct = default);
    Task ConfirmProvisionAsync(Guid ownerId, long amountKurus, string transactionId, CancellationToken ct = default);
    Task ReleaseProvisionAsync(Guid ownerId, long amountKurus, string qrToken, CancellationToken ct = default);
}

public class WalletApplicationService(
    IWalletRepository walletRepo,
    WalletDbContext db) : IWalletService
{
    public async Task<Wallet> CreateWalletAsync(
        Guid ownerId, string ownerType = "CUSTOMER", CancellationToken ct = default)
    {
        var existing = await walletRepo.GetByOwnerIdAsync(ownerId, ct);
        if (existing is not null)
            throw new BusinessRuleException("WALLET_ALREADY_EXISTS", "Cüzdan Mevcut",
                "Bu kullanıcıya ait cüzdan zaten mevcut.", 409);

        var wallet = Wallet.Create(ownerId, ownerType);
        await walletRepo.AddAsync(wallet, ct);
        await walletRepo.SaveChangesAsync(ct);
        return wallet;
    }

    public async Task ActivateWalletAsync(Guid ownerId, CancellationToken ct = default)
    {
        var wallet = await walletRepo.GetByOwnerIdAsync(ownerId, ct)
            ?? throw new NotFoundException("Wallet", ownerId);

        wallet.Activate();
        await walletRepo.SaveChangesAsync(ct);
    }

    public async Task<Wallet> GetBalanceAsync(Guid ownerId, CancellationToken ct = default)
        => await walletRepo.GetByOwnerIdAsync(ownerId, ct)
           ?? throw new NotFoundException("Wallet", ownerId);

    public async Task TopupAsync(
        Guid ownerId, long amountKurus, string referenceId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var wallet = await walletRepo.GetByOwnerIdWithLockAsync(ownerId, ct)
            ?? throw new NotFoundException("Wallet", ownerId);

        wallet.Credit(amountKurus);

        var ledger = WalletLedger.Create(wallet.Id, "TOPUP", amountKurus, wallet.AvailableBalance, referenceId);
        await walletRepo.AddLedgerEntryAsync(ledger, ct);
        await walletRepo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ProvisionAsync(
        Guid ownerId, long amountKurus, string qrToken, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var wallet = await walletRepo.GetByOwnerIdWithLockAsync(ownerId, ct)
            ?? throw new NotFoundException("Wallet", ownerId);

        // Provision işlemi domain entity'de yeterli bakiye kontrolü yapar
        wallet.Provision(amountKurus);

        var ledger = WalletLedger.Create(wallet.Id, "PROVISION", -amountKurus, wallet.AvailableBalance, qrToken);
        await walletRepo.AddLedgerEntryAsync(ledger, ct);
        await walletRepo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ConfirmProvisionAsync(
        Guid ownerId, long amountKurus, string transactionId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var wallet = await walletRepo.GetByOwnerIdWithLockAsync(ownerId, ct)
            ?? throw new NotFoundException("Wallet", ownerId);

        wallet.ConfirmProvision(amountKurus);

        var ledger = WalletLedger.Create(wallet.Id, "CONFIRM", -amountKurus, wallet.AvailableBalance, transactionId);
        await walletRepo.AddLedgerEntryAsync(ledger, ct);
        await walletRepo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ReleaseProvisionAsync(
        Guid ownerId, long amountKurus, string qrToken, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var wallet = await walletRepo.GetByOwnerIdWithLockAsync(ownerId, ct)
            ?? throw new NotFoundException("Wallet", ownerId);

        wallet.ReleaseProvision(amountKurus);

        var ledger = WalletLedger.Create(wallet.Id, "RELEASE", amountKurus, wallet.AvailableBalance, qrToken);
        await walletRepo.AddLedgerEntryAsync(ledger, ct);
        await walletRepo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
