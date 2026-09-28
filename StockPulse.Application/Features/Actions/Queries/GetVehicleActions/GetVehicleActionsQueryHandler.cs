using MediatR;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Application.Features.Actions.Queries.GetVehicleActions;

public class GetVehicleActionsQueryHandler(IVehicleActionLogRepository actionLogRepository)
    : IRequestHandler<GetVehicleActionsQuery, List<VehicleActionLogDto>>
{
    public async Task<List<VehicleActionLogDto>> Handle(GetVehicleActionsQuery request, CancellationToken cancellationToken)
    {
        var logs = await actionLogRepository.GetByVehicleIdAsync(request.VehicleId, cancellationToken);

        return logs.Select(l => new VehicleActionLogDto
        {
            Id = l.Id,
            VehicleId = l.VehicleId,
            ActionType = l.ActionType,
            LoggedAt = l.LoggedAt
        }).ToList();
    }
}
