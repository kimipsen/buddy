using FluentValidation;

namespace buddy.Features.VisitSummaries;

public sealed class UpdateVisitSummaryValidator : AbstractValidator<UpdateVisitSummary>
{
    public UpdateVisitSummaryValidator()
    {
        this.ValidDetails(x => x.Details);
    }
}
