using MediatR;
using StockPulse.Application.Exceptions;
using StockPulse.Application.Features.Actions.Queries.GetVehicleActions;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Interfaces;

namespace StockPulse.Application.Features.Actions.Commands.LogVehicleAction;

public class LogVehicleActionCommandHandler(
    IVehicleRepository vehicleRepository,
    IVehicleActionLogRepository actionLogRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LogVehicleActionCommand, VehicleActionLogDto>
{
    public async Task<VehicleActionLogDto> Handle(LogVehicleActionCommand request, CancellationToken cancellationToken)
    {
        var vehicleExists = await vehicleRepository.ExistsAsync(request.VehicleId, cancellationToken);
        if (!vehicleExists)
            throw new NotFoundException(nameof(Domain.Entities.Vehicle), request.VehicleId);

        var log = new VehicleActionLog
        {
            Id = Guid.NewGuid(),
            VehicleId = request.VehicleId,
            ActionType = request.ActionType,
            LoggedAt = DateTime.UtcNow
        };

        await actionLogRepository.AddAsync(log, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new VehicleActionLogDto
        {
            Id = log.Id,
            VehicleId = log.VehicleId,
            ActionType = log.ActionType,
            LoggedAt = log.LoggedAt
        };
    }
}
