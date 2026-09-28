using MediatR;

namespace StockPulse.Application.Features.Inventory.Queries.GetInventory;

public record GetInventoryQuery(
    string? Make,
    string? Model,
    int? Year,
    bool? IsAging,
    int Page,
    int PageSize) : IRequest<InventoryResponse>;
