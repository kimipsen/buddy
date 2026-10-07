using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

// Meals, the meal plan, AI keys and AI sessions belong to the family, anchored to one child in their
// index documents. An erased child's comments on meals are masked, and what is anchored to them
// passes to their heir (a sibling) -- or is deleted when no sibling is left. An erased guardian's AI
// keys are removed (and their encrypted value masked in the history), and the AI sessions they
// started are masked. Meal plan entries and meals a guardian wrote stay with the family. See
// gdpr-data-protection.md.
public sealed class MealplansPersonalDataEraser(
    IMealplansStore store,
    IMealEventStore meals,
    IAiCredentialEventStore credentials,
    IAiSessionEventStore sessions,
    IGuardianLinkEventStore guardians) : IPersonalDataEraser
{
    public Type Store => typeof(IMealplansStore);

    public static void ConfigureMasking(StoreOptions options)
    {
        // An empty comment already means "no comment".
        options.Events.AddMaskingRuleForProtectedInformation<MealRated>(e => e with { Rating = e.Rating with { Comment = "" } });
        options.Events.AddMaskingRuleForProtectedInformation<ProviderApiKeySet>(e => e with { Key = e.Key with { CipherText = "", Last4 = "" } });
        options.Events.AddMaskingRuleForProtectedInformation<AiSessionStarted>(e => e with { Notes = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<AiUserMessageSent>(e => e with { Text = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<AiAssistantMessageRecorded>(e => e with { Text = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<AiToolInvocationRecorded>(e => e with { ArgumentsJson = "{}", ResultJson = "{}" });
    }

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        var links = await guardians.ListForGuardianAsync(guardian.UserId, cancellationToken);
        var revokedLinks = await guardians.ListRevokedForGuardianAsync(guardian.UserId, cancellationToken);
        var children = links.Concat(revokedLinks).Select(l => new UserId(l.ChildId)).Distinct().ToArray();

        var credentialIds = new HashSet<AiCredentialId>();
        foreach (var child in children)
        {
            if (await credentials.FindIdForChildAsync(child, cancellationToken) is { } credentialId)
            {
                credentialIds.Add(credentialId);
            }
        }

        foreach (var credentialId in credentialIds)
        {
            await RemoveKeysAddedByAsync(credentialId, guardian.UserId, cancellationToken);
        }

        foreach (var sessionId in await ListSessionIdsAsync(children, cancellationToken))
        {
            var events = await sessions.ReadAsync(new MealplanAiSessionId(sessionId), cancellationToken);

            if (events.Any(e => e is AiSessionStarted started && started.StartedBy == guardian.UserId))
            {
                await store.MaskStreamAsync<MealplanAiSessionSnapshot>(sessionId, cancellationToken);
            }
        }
    }

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        if (heir is null)
        {
            await DeleteAnchoredAsync(child.UserId, cancellationToken);
            return;
        }

        await PassAnchoredToAsync(child.UserId, heir, cancellationToken);

        // The family's meals are now found through the heir; the erased child's comments on them go.
        foreach (var mealId in await MealFamilyResolution.ResolveFamilyMealIdsAsync(heir, guardians, meals, cancellationToken))
        {
            await store.MaskStreamAsync<MealSnapshot>(
                mealId.Value,
                cancellationToken,
                e => e.Data is MealRated rated && rated.ChildId == child.UserId);
        }
    }

    private async Task RemoveKeysAddedByAsync(AiCredentialId credentialId, UserId guardianId, CancellationToken cancellationToken)
    {
        if (await credentials.FindSnapshotAsync(credentialId, cancellationToken) is not { } credential)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        List<AiProviderCredentialEvent> removals = [];

        foreach (var (provider, key) in credential.Providers.Where(p => p.Value.AddedBy == guardianId))
        {
            removals.Add(new ProviderApiKeyRemoved(credentialId, provider, guardianId, now));

            if (credential.ActiveProvider == provider)
            {
                removals.Add(new ActiveProviderCleared(credentialId, guardianId, now));
            }
        }

        await credentials.AppendAsync(credentialId, removals, cancellationToken);

        await store.MaskStreamAsync<AiProviderCredentialSnapshot>(
            credentialId.Value,
            cancellationToken,
            e => e.Data is ProviderApiKeySet set && set.Key.AddedBy == guardianId);
    }

    private async Task DeleteAnchoredAsync(UserId childId, CancellationToken cancellationToken)
    {
        var anchor = childId.Value;
        IReadOnlyList<MealIndexDocument> mealDocs;
        IReadOnlyList<MealPlanIndexDocument> planDocs;
        IReadOnlyList<AiCredentialIndexDocument> credentialDocs;
        IReadOnlyList<AiSessionIndexDocument> sessionDocs;
        IReadOnlyList<GroupSharedMealPlanDocument> sharedDocs;

        await using (var session = store.QuerySession())
        {
            mealDocs = await session.Query<MealIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken);
            planDocs = await session.Query<MealPlanIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken);
            credentialDocs = await session.Query<AiCredentialIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken);
            sessionDocs = await session.Query<AiSessionIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken);
            sharedDocs = await session.Query<GroupSharedMealPlanDocument>().Where(d => d.AnchorChildId == anchor).ToListAsync(cancellationToken);
        }

        foreach (var doc in mealDocs)
        {
            await store.DeleteStreamAsync<MealSnapshot>(doc.Id, cancellationToken, s => s.Delete(doc));
        }

        foreach (var doc in planDocs)
        {
            await store.DeleteStreamAsync<MealPlanSnapshot>(doc.Id, cancellationToken, s =>
            {
                s.Delete(doc);

                foreach (var shared in sharedDocs.Where(d => d.Id == doc.Id))
                {
                    s.Delete(shared);
                }
            });
        }

        foreach (var doc in credentialDocs)
        {
            await store.DeleteStreamAsync<AiProviderCredentialSnapshot>(doc.Id, cancellationToken, s => s.Delete(doc));
        }

        foreach (var doc in sessionDocs)
        {
            await store.DeleteStreamAsync<MealplanAiSessionSnapshot>(doc.Id, cancellationToken, s => s.Delete(doc));
        }
    }

    private async Task PassAnchoredToAsync(UserId childId, UserId heir, CancellationToken cancellationToken)
    {
        var anchor = childId.Value;

        await using var session = store.LightweightSession();

        foreach (var doc in await session.Query<MealIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken))
        {
            session.Store(doc with { ChildId = heir.Value });
        }

        foreach (var doc in await session.Query<MealPlanIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken))
        {
            session.Store(doc with { ChildId = heir.Value });
        }

        foreach (var doc in await session.Query<AiCredentialIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken))
        {
            session.Store(doc with { ChildId = heir.Value });
        }

        foreach (var doc in await session.Query<AiSessionIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken))
        {
            session.Store(doc with { ChildId = heir.Value });
        }

        foreach (var doc in await session.Query<GroupSharedMealPlanDocument>().Where(d => d.AnchorChildId == anchor).ToListAsync(cancellationToken))
        {
            session.Store(doc with { AnchorChildId = heir.Value });
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ListSessionIdsAsync(IReadOnlyCollection<UserId> children, CancellationToken cancellationToken)
    {
        var childIds = children.Select(c => c.Value).ToArray();

        await using var session = store.QuerySession();
        return await session.Query<AiSessionIndexDocument>()
            .Where(d => childIds.Contains(d.ChildId))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);
    }
}
