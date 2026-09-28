using StockPulse.Domain.Entities;

namespace StockPulse.Domain.Interfaces;

public interface IVehicleRepository
{
    Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(
        Guid dealershipId,
        string? make,
        string? model,
        int? year,
        bool? isAging,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountAgingAsync(Guid dealershipId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Vehicle> vehicles, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
