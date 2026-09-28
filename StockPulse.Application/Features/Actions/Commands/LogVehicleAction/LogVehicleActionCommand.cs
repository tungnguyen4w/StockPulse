using MediatR;
using StockPulse.Application.Features.Actions.Queries.GetVehicleActions;
using StockPulse.Domain.Enums;

namespace StockPulse.Application.Features.Actions.Commands.LogVehicleAction;

public record LogVehicleActionCommand(Guid VehicleId, VehicleActionType ActionType) : IRequest<VehicleActionLogDto>;
