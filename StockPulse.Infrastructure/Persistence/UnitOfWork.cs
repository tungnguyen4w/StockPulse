using StockPulse.Domain.Interfaces;

namespace StockPulse.Infrastructure.Persistence;

public class UnitOfWork(InventoryDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => context.SaveChangesAsync(cancellationToken);
}
