using StockPulse.Domain.Enums;

namespace StockPulse.Domain.Entities;

public class VehicleActionLog
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public VehicleActionType ActionType { get; set; }
    public DateTime LoggedAt { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}
