using MediatR;
using Microsoft.Extensions.Options;
using StockPulse.Application.Common;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Application.Features.Inventory.Queries.GetInventory;

public class GetInventoryQueryHandler(IVehicleRepository vehicleRepository, IOptions<InventorySettings> settings)
    : IRequestHandler<GetInventoryQuery, InventoryResponse>
{
    public async Task<InventoryResponse> Handle(GetInventoryQuery request, CancellationToken cancellationToken)
    {
        var dealershipId = settings.Value.DealershipId;

        var (items, totalCount) = await vehicleRepository.GetPagedAsync(
            dealershipId,
            request.Make,
            request.Model,
            request.Year,
            request.IsAging,
            request.Page,
            request.PageSize,
            cancellationToken);

        var agingCount = await vehicleRepository.CountAgingAsync(dealershipId, cancellationToken);

        var dtos = items.Select(v => new VehicleDto
        {
            Id = v.Id,
            VIN = v.VIN,
            Make = v.Make,
            Model = v.Model,
            Year = v.Year,
            Colour = v.Colour,
            Price = v.Price,
            Status = v.Status,
            ArrivedAt = v.ArrivedAt,
            IsAging = v.IsAging,
            DealershipId = v.DealershipId
        }).ToList();

        return new InventoryResponse
        {
            Items = dtos,
            PageNumber = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            AgingStockCount = agingCount
        };
    }
}
