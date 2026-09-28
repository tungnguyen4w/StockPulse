using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockPulse.Application.Common;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;
using StockPulse.Domain.Interfaces;
using StockPulse.Infrastructure.Jobs;

namespace StockPulse.API.Controllers;

[ApiController]
[Route("api/admin")]
[AllowAnonymous]
public class AdminController(
    IBackgroundJobClient backgroundJobClient,
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    IOptions<InventorySettings> settings) : ControllerBase
{
    /// <summary>
    /// Triggers the aging stamp job immediately.
    /// Use after seeding data to populate aging flags without waiting for the scheduled run.
    /// </summary>
    [HttpPost("jobs/trigger-aging-stamp")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult TriggerAgingStamp()
    {
        var jobId = backgroundJobClient.Enqueue<AgingStampJob>(j => j.ExecuteAsync());
        return Accepted(new { jobId });
    }

    /// <summary>
    /// Seeds the database with sample vehicles for demo purposes.
    /// Accepts an array of vehicle objects. VINs must be unique.
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Seed([FromBody] List<CreateVehicleRequest> requests, CancellationToken cancellationToken)
    {
        var dealershipId = settings.Value.DealershipId;

        var vehicles = requests.Select(r => new Vehicle
        {
            Id = Guid.NewGuid(),
            VIN = r.VIN,
            Make = r.Make,
            Model = r.Model,
            Year = r.Year,
            Colour = r.Colour,
            Price = r.Price,
            Status = r.Status,
            ArrivedAt = r.ArrivedAt,
            IsAging = false,
            DealershipId = dealershipId
        }).ToList();

        await vehicleRepository.AddRangeAsync(vehicles, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Created(string.Empty, new { seeded = vehicles.Count });
    }
}

public record CreateVehicleRequest(
    string VIN,
    string Make,
    string Model,
    int Year,
    string Colour,
    decimal Price,
    VehicleStatus Status,
    DateTime ArrivedAt);
