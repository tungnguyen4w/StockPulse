using StockPulse.Domain.Enums;

namespace StockPulse.API.Models;

public record LogVehicleActionRequest(VehicleActionType ActionType);
