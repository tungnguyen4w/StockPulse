using FluentValidation;
using StockPulse.Domain.Enums;

namespace StockPulse.Application.Features.Actions.Commands.LogVehicleAction;

public class LogVehicleActionCommandValidator : AbstractValidator<LogVehicleActionCommand>
{
    public LogVehicleActionCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty().WithMessage("VehicleId is required.");
        RuleFor(x => x.ActionType)
            .IsInEnum()
            .WithMessage($"ActionType must be one of: {string.Join(", ", Enum.GetNames<VehicleActionType>())}");
    }
}
