using buddy.Common.Validation;
using buddy.Features.Babysitters;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.NightDuty;

// Limits from docs/backend/analysis/night-duty.md, "Validation limits".
internal static class NightDutyRules
{
    public const int MaxNoteLength = 60;

    // Same limits as a pickup Playdate's free-text fields.
    public const int MaxHostNameLength = 200;
    public const int MaxLocationLength = 200;
    public const int MaxContactInfoLength = 2000;

    // Same cap as the work-location and pickup ranges, so one request never writes or reads more
    // than a month of nights.
    public const int MaxRangeDays = 31;

    // Checks the people an entry names, against the child's current guardian links and the
    // babysitter lists -- the AssignPickup relationship checks. Each distinct responsible person is
    // checked once, and only if the nights being replaced don't already name them (`unchanged`): a
    // night that names a since archived babysitter (or a removed guardian) keeps resolving, so
    // re-saving it -- or switching it -- must not be refused
    // (docs/backend/analysis/babysitters.md, Question 3). Returns a validation problem, or null
    // when every entry is acceptable.
    public static async Task<ValidationProblem?> CheckRelationshipsAsync(
        UserId childId,
        IEnumerable<NightEntry> entries,
        IEnumerable<NightEntry> unchanged,
        IGuardianLinkEventStore guardians,
        IBabysitterListEventStore babysitters,
        CancellationToken cancellationToken)
    {
        var known = unchanged.Select(e => e.Responsible).ToHashSet();
        var responsibles = entries.Select(e => e.Responsible).Distinct().Where(r => !known.Contains(r)).ToList();

        foreach (var responsible in responsibles)
        {
            var problem = responsible switch
            {
                NightResponsible.Guardian guardian =>
                    await guardians.FindActiveLinkAsync(childId, guardian.GuardianId, cancellationToken) is null
                        ? ValidationProblem.Of("guardianId is not an active guardian of this child.")
                        : null,
                NightResponsible.Babysitter babysitter =>
                    await guardians.FindActiveLinkAsync(childId, babysitter.GuardianId, cancellationToken) is null
                        ? ValidationProblem.Of("guardianId is not an active guardian of this child.")
                        : await IsActiveBabysitterAsync(babysitter, babysitters, cancellationToken)
                            ? null
                            : ValidationProblem.Of("babysitterId is not an active babysitter of that guardian."),
                NightResponsible.Sleepover or NightResponsible.Unassigned => null,
            };

            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    // Archived babysitters can't be picked for a new entry; nights already naming one keep resolving
    // (docs/backend/analysis/babysitters.md, Question 3).
    private static async Task<bool> IsActiveBabysitterAsync(NightResponsible.Babysitter babysitter, IBabysitterListEventStore babysitters, CancellationToken cancellationToken) =>
        await babysitters.FindSnapshotAsync(BabysitterListId.ForGuardian(babysitter.GuardianId), cancellationToken) is { } list
            && list.FindActive(babysitter.BabysitterId) is not null;
}

// Structural rules for one entry, shared by ReplaceNightPattern (each day) and SetNightOverrides
// (each planned night). Error keys are camelCase JSON names.
public sealed class NightEntryValidator : AbstractValidator<NightEntry>
{
    public NightEntryValidator()
    {
        RuleFor(e => e.Note)
            .MaximumLength(NightDutyRules.MaxNoteLength)
            .OverridePropertyName("note");

        RuleFor(e => e)
            .Must(e => !e.IsEmpty)
            .WithMessage("A night needs a responsible person, a note, or both.")
            .OverridePropertyName("responsible");

        RuleFor(e => e.Responsible)
            .Must(r => r is not NightResponsible.Sleepover sleepover || sleepover.HostName.Length > 0)
            .WithMessage("A sleepover requires a hostName.")
            .Must(r => r is not NightResponsible.Sleepover sleepover || sleepover.HostName.Length <= NightDutyRules.MaxHostNameLength)
            .WithMessage($"hostName can be at most {NightDutyRules.MaxHostNameLength} characters.")
            .Must(r => r is not NightResponsible.Sleepover sleepover || sleepover.Location.Length <= NightDutyRules.MaxLocationLength)
            .WithMessage($"location can be at most {NightDutyRules.MaxLocationLength} characters.")
            .Must(r => r is not NightResponsible.Sleepover sleepover || sleepover.ContactInfo.Length <= NightDutyRules.MaxContactInfoLength)
            .WithMessage($"contactInfo can be at most {NightDutyRules.MaxContactInfoLength} characters.")
            .OverridePropertyName("responsible");
    }
}
