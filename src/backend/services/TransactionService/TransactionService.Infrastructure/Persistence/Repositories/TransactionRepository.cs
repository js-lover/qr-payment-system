// TransactionService.Infrastructure / Persistence / Repositories / TransactionRepository.cs
//
// ITransactionRepository EF Core implementasyonu.

using Microsoft.EntityFrameworkCore;
using TransactionService.Domain.Entities;
using TransactionService.Domain.Interfaces;

namespace TransactionService.Infrastructure.Persistence.Repositories;

public class TransactionRepository(TransactionDbContext db) : ITransactionRepository
{
    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Transaction?> GetByQrTokenAsync(string qrToken, CancellationToken ct = default)
        => db.Transactions.FirstOrDefaultAsync(t => t.QrToken == qrToken, ct);

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
        => await db.Transactions.AddAsync(transaction, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
