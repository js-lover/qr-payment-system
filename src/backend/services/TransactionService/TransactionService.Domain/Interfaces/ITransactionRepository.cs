// TransactionService.Domain / Interfaces / ITransactionRepository.cs
//
// İşlem repository arayüzü.

using TransactionService.Domain.Entities;

namespace TransactionService.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Transaction?> GetByQrTokenAsync(string qrToken, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
