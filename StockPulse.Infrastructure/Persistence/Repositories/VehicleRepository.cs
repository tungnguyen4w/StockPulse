using Microsoft.EntityFrameworkCore;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Infrastructure.Persistence.Repositories;

public class VehicleRepository(InventoryDbContext context) : IVehicleRepository
{
    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(
        Guid dealershipId, string? make, string? model, int? year, bool? isAging,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.Vehicles.Where(v => v.DealershipId == dealershipId);

        if (!string.IsNullOrWhiteSpace(make))
            query = query.Where(v => v.Make.ToLower() == make.ToLower());

        if (!string.IsNullOrWhiteSpace(model))
            query = query.Where(v => v.Model.ToLower() == model.ToLower());

        if (year.HasValue)
            query = query.Where(v => v.Year == year.Value);

        if (isAging.HasValue)
            query = query.Where(v => v.IsAging == isAging.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(v => v.ArrivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Vehicles.FindAsync([id], cancellationToken);

    public async Task<int> CountAgingAsync(Guid dealershipId, CancellationToken cancellationToken = default)
        => await context.Vehicles.CountAsync(v => v.DealershipId == dealershipId && v.IsAging, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Vehicle> vehicles, CancellationToken cancellationToken = default)
        => await context.Vehicles.AddRangeAsync(vehicles, cancellationToken);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Vehicles.AnyAsync(v => v.Id == id, cancellationToken);
}
