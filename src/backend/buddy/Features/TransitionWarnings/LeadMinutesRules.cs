using System.Collections.Immutable;

using FluentValidation;

namespace buddy.Features.TransitionWarnings;

// 1-3 distinct lead times of 1-120 minutes, on the defaults and on an item override. An override
// may be empty: that is "never warn for this item".
public static class LeadMinutesRules
{
    public static IRuleBuilderOptions<T, ImmutableArray<int>> ValidLeadMinutes<T>(this IRuleBuilder<T, ImmutableArray<int>> rule, bool allowEmpty) =>
        rule
            .Must(minutes => allowEmpty || minutes.Length > 0).WithMessage("Choose at least one lead time.")
            .Must(minutes => minutes.Length <= LeadTimeRules.MaxCount).WithMessage($"Choose at most {LeadTimeRules.MaxCount} lead times.")
            .Must(minutes => minutes.Distinct().Count() == minutes.Length).WithMessage("Lead times must be different from each other.")
            .Must(minutes => minutes.All(m => m is >= LeadTimeRules.MinMinutes and <= LeadTimeRules.MaxMinutes))
            .WithMessage($"Lead times must be between {LeadTimeRules.MinMinutes} and {LeadTimeRules.MaxMinutes} minutes.");
}
