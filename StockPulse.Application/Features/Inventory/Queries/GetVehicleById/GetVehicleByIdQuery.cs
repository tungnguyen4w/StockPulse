using MediatR;
using StockPulse.Application.Features.Inventory.Queries.GetInventory;

namespace StockPulse.Application.Features.Inventory.Queries.GetVehicleById;

public record GetVehicleByIdQuery(Guid Id) : IRequest<VehicleDto?>;
