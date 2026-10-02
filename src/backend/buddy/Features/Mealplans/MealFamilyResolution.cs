using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Meals and a MealPlan are shared by every child who has at least one active guardian in common
// with the requested child -- so two siblings never need two separate meal libraries or plans.
// There is no persisted "family"/"household" concept anywhere in this codebase (see
// docs/backend/analysis/mealplans.md), so sibling membership is recomputed from the existing
// GuardianLink graph on every call rather than stored -- the same "recomputed, not persisted"
// contract MealPlanExpansion already has for occurrences.
public static class MealFamilyResolution
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

    // Every Meal is indexed under whichever single child its guardian happened to be acting on
    // behalf of when it was created (see MartenMealEventStore.CreateAsync) -- sharing across
    // siblings happens entirely here, by widening the lookup to the whole family rather than by
    // writing extra index rows at creation time.
    public static async Task<IReadOnlyCollection<MealId>> ResolveFamilyMealIdsAsync(
        UserId childId, IGuardianLinkEventStore guardians, IMealEventStore meals, CancellationToken cancellationToken)
    {
        var family = await ResolveFamilyAsync(childId, guardians, cancellationToken);
        var mealIds = new HashSet<MealId>();

        foreach (var member in family)
        {
            mealIds.UnionWith(await meals.ListIdsForChildAsync(member, cancellationToken));
        }

        return mealIds;
    }

    // A MealPlan is a family-wide singleton, so at most one sibling's index row should ever exist
    // in correct operation -- the first one found is returned.
    public static async Task<MealPlanId?> ResolveFamilyMealPlanIdAsync(
        UserId childId, IGuardianLinkEventStore guardians, IMealPlanEventStore mealPlans, CancellationToken cancellationToken)
    {
        var family = await ResolveFamilyAsync(childId, guardians, cancellationToken);

        foreach (var member in family)
        {
            if (await mealPlans.FindIdForChildAsync(member, cancellationToken) is { } id)
            {
                return id;
            }
        }

        return null;
    }

    // An AiProviderCredential is a family-wide resource, but its index row
    // (AiCredentialIndexDocument) names exactly one child -- the one the first key was added for.
    // Resolution therefore has to look wider than that one child, and has to pick
    // deterministically when more than one credential is reachable. The rule
    // (docs/backend/mealplans/flow.md, "AI credential resolution"):
    //
    // Candidates are the credentials indexed under
    //   1. any child in the requested child's family (ResolveFamilyAsync -- every child of every
    //      active guardian of that child, which includes every child the caller is linked to), and
    //   2. any child the calling guardian has since unlinked (a revoked GuardianLink), but only if
    //      the caller contributed to that credential themselves (added a key or changed the active
    //      provider). Unlinking the child that happened to hold the index row therefore doesn't
    //      drop the key for the guardian's remaining children, while a key set up entirely by
    //      someone else in a family the caller has left is not inherited.
    //
    // The winner is the most recently activated candidate: the one whose current active provider
    // was set last (OccurredAt of its latest ActiveProviderChanged, unless an ActiveProviderCleared
    // came after it). Credentials with no active provider rank below every activated one. Ties, and the
    // order among never-/no-longer-activated credentials, fall back to the larger (newer, UUIDv7)
    // credential id. This replaces "first index row found", which over a HashSet was arbitrary as
    // soon as two families with a credential each merged.
    public static async Task<AiCredentialId?> ResolveFamilyAiCredentialIdAsync(
        UserId childId,
        UserId guardianId,
        IGuardianLinkEventStore guardians,
        IAiCredentialEventStore aiCredentials,
        CancellationToken cancellationToken)
    {
        var family = await ResolveFamilyAsync(childId, guardians, cancellationToken);
        var unlinked = (await guardians.ListRevokedForGuardianAsync(guardianId, cancellationToken))
            .Select(link => new UserId(link.ChildId))
            .Where(child => !family.Contains(child))
            .ToHashSet();

        (AiCredentialId Id, DateTimeOffset? ActivatedAt)? best = null;
        var seen = new HashSet<AiCredentialId>();

        foreach (var (member, requiresContribution) in family.Select(m => (m, false)).Concat(unlinked.Select(m => (m, true))))
        {
            if (await aiCredentials.FindIdForChildAsync(member, cancellationToken) is not { } id || !seen.Add(id))
            {
                continue;
            }

            var events = await aiCredentials.ReadAsync(id, cancellationToken);

            if (requiresContribution && !HasContributed(events, guardianId))
            {
                continue;
            }

            var candidate = (id, LastActivatedAt(events));

            if (best is null || IsPreferred(candidate, best.Value))
            {
                best = candidate;
            }
        }

        return best?.Id;
    }

    // OccurredAt of the ActiveProviderChanged that set the current active provider, or null when
    // the credential has no active provider right now (never set, or cleared by removing the
    // active provider's key).
    private static DateTimeOffset? LastActivatedAt(IEnumerable<AiProviderCredentialEvent> events)
    {
        DateTimeOffset? activatedAt = null;

        foreach (var @event in events)
        {
            activatedAt = @event switch
            {
                ActiveProviderChanged changed => changed.OccurredAt,
                ActiveProviderCleared => null,
                AiCredentialsInitialized or ProviderApiKeySet or ProviderApiKeyRemoved => activatedAt,
            };
        }

        return activatedAt;
    }

    private static bool HasContributed(IEnumerable<AiProviderCredentialEvent> events, UserId guardianId) =>
        events.Any(e => e switch
        {
            ProviderApiKeySet set => set.Key.AddedBy == guardianId,
            ActiveProviderChanged changed => changed.ChangedBy == guardianId,
            ActiveProviderCleared cleared => cleared.ClearedBy == guardianId,
            ProviderApiKeyRemoved removed => removed.RemovedBy == guardianId,
            AiCredentialsInitialized => false,
        });

    private static bool IsPreferred((AiCredentialId Id, DateTimeOffset? ActivatedAt) candidate, (AiCredentialId Id, DateTimeOffset? ActivatedAt) current) =>
        (candidate.ActivatedAt, current.ActivatedAt) switch
        {
            ({ } a, { } b) when a != b => a > b,
            ({ }, null) => true,
            (null, { }) => false,
            _ => candidate.Id.Value.CompareTo(current.Id.Value) > 0,
        };
}
