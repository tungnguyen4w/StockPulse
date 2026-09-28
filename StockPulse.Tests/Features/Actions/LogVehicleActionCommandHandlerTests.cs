using FluentAssertions;
using Moq;
using StockPulse.Application.Exceptions;
using StockPulse.Application.Features.Actions.Commands.LogVehicleAction;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Tests.Features.Actions;

public class LogVehicleActionCommandHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock = new();
    private readonly Mock<IVehicleActionLogRepository> _logRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly LogVehicleActionCommandHandler _handler;

    public LogVehicleActionCommandHandlerTests()
    {
        _handler = new LogVehicleActionCommandHandler(
            _vehicleRepoMock.Object,
            _logRepoMock.Object,
            _uowMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesAndReturnsLogEntry()
    {
        var vehicleId = Guid.NewGuid();
        _vehicleRepoMock.Setup(r => r.ExistsAsync(vehicleId, default)).ReturnsAsync(true);
        _logRepoMock.Setup(r => r.AddAsync(It.IsAny<VehicleActionLog>(), default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var result = await _handler.Handle(
            new LogVehicleActionCommand(vehicleId, VehicleActionType.PriceReductionPlanned), default);

        result.Should().NotBeNull();
        result.VehicleId.Should().Be(vehicleId);
        result.ActionType.Should().Be(VehicleActionType.PriceReductionPlanned);
        result.LoggedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_VehicleNotFound_ThrowsNotFoundException()
    {
        var unknownId = Guid.NewGuid();
        _vehicleRepoMock.Setup(r => r.ExistsAsync(unknownId, default)).ReturnsAsync(false);

        var act = async () => await _handler.Handle(
            new LogVehicleActionCommand(unknownId, VehicleActionType.DiscountApplied), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AppendsLog_DoesNotReplaceExisting()
    {
        var vehicleId = Guid.NewGuid();
        _vehicleRepoMock.Setup(r => r.ExistsAsync(vehicleId, default)).ReturnsAsync(true);

        VehicleActionLog? capturedLog = null;
        _logRepoMock.Setup(r => r.AddAsync(It.IsAny<VehicleActionLog>(), default))
            .Callback<VehicleActionLog, CancellationToken>((log, _) => capturedLog = log)
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        await _handler.Handle(new LogVehicleActionCommand(vehicleId, VehicleActionType.WriteOffScheduled), default);

        capturedLog.Should().NotBeNull();
        capturedLog!.Id.Should().NotBe(Guid.Empty);
        _logRepoMock.Verify(r => r.AddAsync(It.IsAny<VehicleActionLog>(), default), Times.Once);
    }
}
