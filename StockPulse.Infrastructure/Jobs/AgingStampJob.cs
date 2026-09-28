using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockPulse.Domain.Constants;
using StockPulse.Infrastructure.Persistence;

namespace StockPulse.Infrastructure.Jobs;

public class AgingStampJob(InventoryDbContext context, ILogger<AgingStampJob> logger, IMeterFactory meterFactory)
{
    private readonly Counter<int> _agingStampedCounter =
        meterFactory.Create("StockPulse").CreateCounter<int>("vehicles.aged.stamped.total");

    public async Task ExecuteAsync()
    {
        var threshold = DateTime.UtcNow.AddDays(-AgingConstants.AgingThresholdDays);

        var count = await context.Vehicles
            .Where(v => v.ArrivedAt < threshold && !v.IsAging)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsAging, true));

        _agingStampedCounter.Add(count);
        logger.LogInformation("Aging stamp complete. {VehiclesStamped} vehicles flagged as aging.", count);
    }
}
