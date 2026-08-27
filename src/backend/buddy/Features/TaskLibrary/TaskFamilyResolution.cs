using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Task templates are shared by every child who has at least one active guardian in common with
// the requested child -- so two siblings never need two separate task libraries. There is no
// persisted "family"/"household" concept anywhere in this codebase (see
// docs/backend/analysis/mealplans.md), so sibling membership is recomputed from the existing
// GuardianLink graph on every call rather than stored -- the same "recomputed, not persisted"
// contract MealFamilyResolution already has.
public static class TaskFamilyResolution
{
    public static async Task<IReadOnlyCollection<UserId>> ResolveFamilyAsync(UserId childId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var family = new HashSet<UserId> { childId };
        var guardianLinks = await guardians.ListForChildAsync(childId, cancellationToken);

        foreach (var guardianLink in guardianLinks)
        {
            var siblingLinks = await guardians.ListForGuardianAsync(new UserId(guardianLink.GuardianId), cancellationToken);

            foreach (var siblingLink in siblingLinks)
            {
                family.Add(new UserId(siblingLink.ChildId));
            }
        }

        return family;
    }

    // Every TaskTemplate is indexed under whichever single child its guardian happened to be
    // acting on behalf of when it was created (see MartenTaskTemplateEventStore.CreateAsync) --
    // sharing across siblings happens entirely here, by widening the lookup to the whole family
    // rather than by writing extra index rows at creation time.
    public static async Task<IReadOnlyCollection<TaskTemplateId>> ResolveFamilyTaskTemplateIdsAsync(
        UserId childId, IGuardianLinkEventStore guardians, ITaskTemplateEventStore templates, CancellationToken cancellationToken)
    {
        var family = await ResolveFamilyAsync(childId, guardians, cancellationToken);
        var templateIds = new HashSet<TaskTemplateId>();

        foreach (var member in family)
        {
            templateIds.UnionWith(await templates.ListIdsForChildAsync(member, cancellationToken));
        }

        return templateIds;
    }
}
