using StockPulse.Domain.Entities;

namespace StockPulse.Domain.Interfaces;

public interface IVehicleActionLogRepository
{
    Task<IReadOnlyList<VehicleActionLog>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task AddAsync(VehicleActionLog log, CancellationToken cancellationToken = default);
}
