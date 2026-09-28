using FluentAssertions;
using Moq;
using StockPulse.Application.Features.Actions.Queries.GetVehicleActions;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Tests.Features.Actions;

public class GetVehicleActionsQueryHandlerTests
{
    private readonly Mock<IVehicleActionLogRepository> _repoMock = new();
    private readonly GetVehicleActionsQueryHandler _handler;

    public GetVehicleActionsQueryHandlerTests()
    {
        _handler = new GetVehicleActionsQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsActionsInChronologicalOrder()
    {
        var vehicleId = Guid.NewGuid();
        var logs = new List<VehicleActionLog>
        {
            new() { Id = Guid.NewGuid(), VehicleId = vehicleId, ActionType = VehicleActionType.NoActionRequired, LoggedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), VehicleId = vehicleId, ActionType = VehicleActionType.PriceReductionPlanned, LoggedAt = DateTime.UtcNow.AddDays(-1) }
        };
        _repoMock.Setup(r => r.GetByVehicleIdAsync(vehicleId, default)).ReturnsAsync(logs.AsReadOnly());

        var result = await _handler.Handle(new GetVehicleActionsQuery(vehicleId), default);

        result.Should().HaveCount(2);
        result[0].ActionType.Should().Be(VehicleActionType.NoActionRequired);
        result[1].ActionType.Should().Be(VehicleActionType.PriceReductionPlanned);
    }

    [Fact]
    public async Task Handle_NoActions_ReturnsEmptyList()
    {
        var vehicleId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByVehicleIdAsync(vehicleId, default))
            .ReturnsAsync(new List<VehicleActionLog>().AsReadOnly());

        var result = await _handler.Handle(new GetVehicleActionsQuery(vehicleId), default);

        result.Should().BeEmpty();
    }
}
