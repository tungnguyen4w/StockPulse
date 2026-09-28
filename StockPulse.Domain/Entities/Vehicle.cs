using StockPulse.Domain.Enums;

namespace StockPulse.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public string VIN { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Colour { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public VehicleStatus Status { get; set; }
    public DateTime ArrivedAt { get; set; }
    public bool IsAging { get; set; }
    public Guid DealershipId { get; set; }

    public ICollection<VehicleActionLog> ActionLogs { get; set; } = new List<VehicleActionLog>();
}
