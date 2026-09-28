namespace StockPulse.Application.Features.Inventory.Queries.GetInventory;

public class InventoryResponse
{
    public IReadOnlyList<VehicleDto> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public int AgingStockCount { get; init; }
}
