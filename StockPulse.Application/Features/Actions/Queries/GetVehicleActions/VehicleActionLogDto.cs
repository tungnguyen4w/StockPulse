using StockPulse.Domain.Enums;

namespace StockPulse.Application.Features.Actions.Queries.GetVehicleActions;

public class VehicleActionLogDto
{
    public Guid Id { get; init; }
    public Guid VehicleId { get; init; }
    public VehicleActionType ActionType { get; init; }
    public DateTime LoggedAt { get; init; }
}
