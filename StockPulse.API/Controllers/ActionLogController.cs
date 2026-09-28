using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPulse.API.Models;
using StockPulse.Application.Features.Actions.Commands.LogVehicleAction;
using StockPulse.Application.Features.Actions.Queries.GetVehicleActions;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("api/inventory/{vehicleId:guid}/actions")]
[AllowAnonymous]
public class ActionLogController(IMediator mediator) : ControllerBase
{
    /// <summary>Returns all actions logged for a vehicle in chronological order.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<VehicleActionLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActions(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetVehicleActionsQuery(vehicleId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Logs a new action for a vehicle. Appended to the audit trail — never replaces existing actions.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VehicleActionLogDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LogAction(
        Guid vehicleId,
        [FromBody] LogVehicleActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new LogVehicleActionCommand(vehicleId, request.ActionType), cancellationToken);
        return CreatedAtAction(nameof(GetActions), new { vehicleId }, result);
    }
}
