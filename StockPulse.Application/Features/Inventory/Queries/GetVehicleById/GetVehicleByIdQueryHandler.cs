using MediatR;
using StockPulse.Application.Features.Inventory.Queries.GetInventory;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Application.Features.Inventory.Queries.GetVehicleById;

public class GetVehicleByIdQueryHandler(IVehicleRepository vehicleRepository)
    : IRequestHandler<GetVehicleByIdQuery, VehicleDto?>
{
    public async Task<VehicleDto?> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (vehicle is null) return null;

        return new VehicleDto
        {
            Id = vehicle.Id,
            VIN = vehicle.VIN,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            Colour = vehicle.Colour,
            Price = vehicle.Price,
            Status = vehicle.Status,
            ArrivedAt = vehicle.ArrivedAt,
            IsAging = vehicle.IsAging,
            DealershipId = vehicle.DealershipId
        };
    }
}
