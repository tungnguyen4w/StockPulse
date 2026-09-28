using StockPulse.Domain.Enums;

namespace StockPulse.Application.Features.Inventory.Queries.GetInventory;

public class VehicleDto
{
    public Guid Id { get; init; }
    public string VIN { get; init; } = string.Empty;
    public string Make { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int Year { get; init; }
    public string Colour { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public VehicleStatus Status { get; init; }
    public DateTime ArrivedAt { get; init; }
    public bool IsAging { get; init; }
    public Guid DealershipId { get; init; }
    public int DaysInInventory => (int)(DateTime.UtcNow - ArrivedAt).TotalDays;
}
