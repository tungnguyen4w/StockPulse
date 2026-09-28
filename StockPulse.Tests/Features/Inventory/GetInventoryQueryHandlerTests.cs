using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using StockPulse.Application.Common;
using StockPulse.Application.Features.Inventory.Queries.GetInventory;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Tests.Features.Inventory;

public class GetInventoryQueryHandlerTests
{
    private readonly Mock<IVehicleRepository> _repoMock = new();
    private readonly Guid _dealershipId = Guid.NewGuid();
    private readonly GetInventoryQueryHandler _handler;

    public GetInventoryQueryHandlerTests()
    {
        var settings = Options.Create(new InventorySettings { DealershipId = _dealershipId });
        _handler = new GetInventoryQueryHandler(_repoMock.Object, settings);
    }

    [Fact]
    public async Task Handle_ReturnsPagedResult_WithAgingStockCount()
    {
        var vehicles = new List<Vehicle>
        {
            new() { Id = Guid.NewGuid(), VIN = "VIN001", Make = "Toyota", Model = "Camry", Year = 2020, IsAging = true, DealershipId = _dealershipId, ArrivedAt = DateTime.UtcNow.AddDays(-100) }
        };

        _repoMock.Setup(r => r.GetPagedAsync(_dealershipId, null, null, null, null, 1, 20, default))
            .ReturnsAsync((vehicles.AsReadOnly(), 1));
        _repoMock.Setup(r => r.CountAgingAsync(_dealershipId, default)).ReturnsAsync(1);

        var query = new GetInventoryQuery(null, null, null, null, 1, 20);
        var result = await _handler.Handle(query, default);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.AgingStockCount.Should().Be(1);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_EmptyInventory_ReturnsZeroAgingCount()
    {
        _repoMock.Setup(r => r.GetPagedAsync(_dealershipId, null, null, null, null, 1, 20, default))
            .ReturnsAsync((new List<Vehicle>().AsReadOnly(), 0));
        _repoMock.Setup(r => r.CountAgingAsync(_dealershipId, default)).ReturnsAsync(0);

        var result = await _handler.Handle(new GetInventoryQuery(null, null, null, null, 1, 20), default);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.AgingStockCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesFiltersToRepository()
    {
        _repoMock.Setup(r => r.GetPagedAsync(_dealershipId, "Toyota", "Camry", 2022, true, 2, 10, default))
            .ReturnsAsync((new List<Vehicle>().AsReadOnly(), 0));
        _repoMock.Setup(r => r.CountAgingAsync(_dealershipId, default)).ReturnsAsync(0);

        await _handler.Handle(new GetInventoryQuery("Toyota", "Camry", 2022, true, 2, 10), default);

        _repoMock.Verify(r => r.GetPagedAsync(_dealershipId, "Toyota", "Camry", 2022, true, 2, 10, default), Times.Once);
    }
}
