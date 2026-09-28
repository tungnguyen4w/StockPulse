using MediatR;

namespace StockPulse.Application.Features.Actions.Queries.GetVehicleActions;

public record GetVehicleActionsQuery(Guid VehicleId) : IRequest<List<VehicleActionLogDto>>;
