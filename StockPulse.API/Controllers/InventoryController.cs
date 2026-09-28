using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.Application.Features.Inventory.Queries.GetInventory;
using StockPulse.Application.Features.Inventory.Queries.GetVehicleById;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("api/inventory")]
[AllowAnonymous]
public class InventoryController(IMediator mediator) : ControllerBase
{
    /// <summary>Returns a paginated, filterable list of vehicles. Always includes agingStockCount.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(InventoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetInventory(
        [FromQuery] string? make,
        [FromQuery] string? model,
        [FromQuery] int? year,
        [FromQuery] bool? isAging,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetInventoryQuery(make, model, year, isAging, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single vehicle by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVehicleById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetVehicleByIdQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
