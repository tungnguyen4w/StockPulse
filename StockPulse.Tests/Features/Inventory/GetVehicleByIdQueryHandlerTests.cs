using FluentAssertions;
using Moq;
using StockPulse.Application.Features.Inventory.Queries.GetVehicleById;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Tests.Features.Inventory;

public class GetVehicleByIdQueryHandlerTests
{
    private readonly Mock<IVehicleRepository> _repoMock = new();
    private readonly GetVehicleByIdQueryHandler _handler;

    public GetVehicleByIdQueryHandlerTests()
    {
        _handler = new GetVehicleByIdQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_VehicleExists_ReturnsDtoWithAllFields()
    {
        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = vehicleId, VIN = "VIN123", Make = "Ford", Model = "Focus",
            Year = 2021, Colour = "Blue", Price = 25000m,
            ArrivedAt = DateTime.UtcNow.AddDays(-30), IsAging = false,
            DealershipId = Guid.NewGuid()
        };
        _repoMock.Setup(r => r.GetByIdAsync(vehicleId, default)).ReturnsAsync(vehicle);

        var result = await _handler.Handle(new GetVehicleByIdQuery(vehicleId), default);

        result.Should().NotBeNull();
        result!.Id.Should().Be(vehicleId);
        result.VIN.Should().Be("VIN123");
        result.Make.Should().Be("Ford");
    }

    [Fact]
    public async Task Handle_VehicleNotFound_ReturnsNull()
    {
        var unknownId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(unknownId, default)).ReturnsAsync((Vehicle?)null);

        var result = await _handler.Handle(new GetVehicleByIdQuery(unknownId), default);

        result.Should().BeNull();
    }
}
