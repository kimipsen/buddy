using FluentValidation;

namespace buddy.Features.VisitSummaries;

public sealed class CreateVisitSummaryValidator : AbstractValidator<CreateVisitSummary>
{
    public CreateVisitSummaryValidator()
    {
        this.ValidDetails(x => x.Details);
    }
}
