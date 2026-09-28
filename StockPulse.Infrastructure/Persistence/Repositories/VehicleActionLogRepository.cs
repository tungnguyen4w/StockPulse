using Microsoft.EntityFrameworkCore;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Infrastructure.Persistence.Repositories;

public class VehicleActionLogRepository(InventoryDbContext context) : IVehicleActionLogRepository
{
    public async Task<IReadOnlyList<VehicleActionLog>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
        => await context.VehicleActionLogs
            .Where(l => l.VehicleId == vehicleId)
            .OrderBy(l => l.LoggedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(VehicleActionLog log, CancellationToken cancellationToken = default)
        => await context.VehicleActionLogs.AddAsync(log, cancellationToken);
}
