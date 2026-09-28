using FluentValidation;

namespace StockPulse.Application.Features.Inventory.Queries.GetInventory;

public class GetInventoryQueryValidator : AbstractValidator<GetInventoryQuery>
{
    public GetInventoryQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}
