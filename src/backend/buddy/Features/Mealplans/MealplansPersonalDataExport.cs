using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

// The "mealplans" section. Meals, the meal plan, the AI keys and the AI sessions belong to a family
// (every child sharing a guardian, see MealFamilyResolution), so the section is per family rather
// than per child: everything reachable from any of the caller's children, once each. AI keys show
// their last four characters only, never the encrypted key; meal plans carry no iCal token hashes.
public sealed class MealplansPersonalDataExporter(
    IMealplansStore store,
    IGuardianLinkEventStore guardians,
    IMealEventStore meals,
    IMealPlanEventStore mealPlans,
    IAiCredentialEventStore credentials,
    IAiSessionEventStore sessions) : IPersonalDataExporter
{
    public Type Store => typeof(IMealplansStore);

    public string Section => "mealplans";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        var family = new HashSet<UserId>();

        foreach (var childId in subject.Children)
        {
            family.UnionWith(await MealFamilyResolution.ResolveFamilyAsync(childId, guardians, cancellationToken));
        }

        var mealIds = new HashSet<MealId>();
        var planIds = new HashSet<MealPlanId>();
        var credentialIds = new HashSet<AiCredentialId>();

        foreach (var member in family)
        {
            mealIds.UnionWith(await meals.ListIdsForChildAsync(member, cancellationToken));

            if (await mealPlans.FindIdForChildAsync(member, cancellationToken) is { } planId)
            {
                planIds.Add(planId);
            }

            if (await credentials.FindIdForChildAsync(member, cancellationToken) is { } credentialId)
            {
                credentialIds.Add(credentialId);
            }
        }

        List<MealResponse> exportedMeals = [];

        foreach (var mealId in mealIds)
        {
            if (await meals.FindSnapshotAsync(mealId, cancellationToken) is { } meal)
            {
                exportedMeals.Add(MealResponse.FromMeal(meal));
            }
        }

        List<ExportedMealPlan> exportedPlans = [];

        foreach (var planId in planIds)
        {
            if (await mealPlans.FindSnapshotAsync(planId, cancellationToken) is { } plan)
            {
                exportedPlans.Add(new ExportedMealPlan(
                    plan.Id.Value,
                    plan.SharedWithGroupId?.Value,
                    [.. plan.SlotTimes.OrderBy(s => s.Key).Select(s => new ExportedSlotTime(s.Key, s.Value))],
                    [.. plan.Assignments.OrderBy(a => a.Key.Date).ThenBy(a => a.Key.Slot).Select(a => new ExportedMealPlanAssignment(
                        a.Key.Date, a.Key.Slot, a.Value.MealId.Value, a.Value.Notes, a.Value.AssignedBy.Value))]));
            }
        }

        List<AiProviderSettings> exportedKeys = [];

        foreach (var credentialId in credentialIds)
        {
            if (await credentials.FindSnapshotAsync(credentialId, cancellationToken) is { } credential)
            {
                exportedKeys.Add(AiProviderSettings.FromCredential(credential));
            }
        }

        return new MealplansExport(
            [.. exportedMeals.OrderBy(m => m.Name, StringComparer.Ordinal)],
            exportedPlans,
            exportedKeys,
            await ExportSessionsAsync(family, cancellationToken));
    }

    private async Task<IReadOnlyList<ExportedAiSession>> ExportSessionsAsync(HashSet<UserId> family, CancellationToken cancellationToken)
    {
        var anchors = family.Select(m => m.Value).ToArray();
        IReadOnlyList<AiSessionIndexDocument> documents;

        await using (var session = store.QuerySession())
        {
            documents = await session.Query<AiSessionIndexDocument>()
                .Where(d => anchors.Contains(d.ChildId))
                .OrderBy(d => d.StartedAt)
                .ToListAsync(cancellationToken);
        }

        List<ExportedAiSession> exported = [];

        foreach (var document in documents)
        {
            var id = new MealplanAiSessionId(document.Id);

            if (await sessions.FindSnapshotAsync(id, cancellationToken) is { } aiSession)
            {
                var view = await AiSessionViewBuilder.BuildAsync(aiSession, await sessions.ReadAsync(id, cancellationToken), meals, cancellationToken);
                exported.Add(new ExportedAiSession(document.StartedAt, view));
            }
        }

        return exported;
    }
}

public sealed record MealplansExport(
    IReadOnlyList<MealResponse> Meals,
    IReadOnlyList<ExportedMealPlan> MealPlans,
    IReadOnlyList<AiProviderSettings> AiProviderKeys,
    IReadOnlyList<ExportedAiSession> AiSessions);

public sealed record ExportedMealPlan(
    Guid Id,
    Guid? SharedWithGroupId,
    IReadOnlyList<ExportedSlotTime> SlotTimes,
    IReadOnlyList<ExportedMealPlanAssignment> Assignments);

public sealed record ExportedSlotTime(MealSlot Slot, TimeOnly Time);

public sealed record ExportedMealPlanAssignment(DateOnly Date, MealSlot Slot, Guid MealId, string Notes, Guid AssignedBy);

public sealed record ExportedAiSession(DateTimeOffset StartedAt, AiSessionView Session);
