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
    IMealieConnectionEventStore mealieConnections,
    IGuardianLinkEventStore guardians,
    TimeProvider time) : IPersonalDataEraser
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
        // The family's server address and Mealie user name can identify them; the token goes too.
        options.Events.AddMaskingRuleForProtectedInformation<MealieConnected>(e => e with
        {
            BaseUrl = Erased.Text,
            Token = e.Token with { CipherText = "", Last4 = "" },
            MealieUserName = Erased.Text,
            Household = e.Household with { HouseholdName = Erased.Text }
        });
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

        // Through each child's whole family: a connection is indexed under the child it was first
        // made for, which needn't be one of this guardian's own children (a sibling under a
        // co-guardian), while the guardian may still have reconnected it.
        var connectionIds = new HashSet<MealieConnectionId>();
        foreach (var child in children)
        {
            foreach (var member in await MealFamilyResolution.ResolveFamilyAsync(child, guardians, cancellationToken))
            {
                if (await mealieConnections.FindIdForChildAsync(member, cancellationToken) is { } connectionId)
                {
                    connectionIds.Add(connectionId);
                }
            }
        }

        foreach (var connectionId in connectionIds)
        {
            await DisconnectMealieConnectedByAsync(connectionId, guardian.UserId, cancellationToken);
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

        var now = time.GetUtcNow();
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

    // A connection the erased guardian made is disconnected (their token stops being used), and every
    // connect of theirs is masked in the history. One made by another guardian stays.
    private async Task DisconnectMealieConnectedByAsync(MealieConnectionId connectionId, UserId guardianId, CancellationToken cancellationToken)
    {
        if (await mealieConnections.FindSnapshotAsync(connectionId, cancellationToken) is not { } connection)
        {
            return;
        }

        if (connection.Current?.ConnectedBy == guardianId)
        {
            await mealieConnections.AppendAsync(connectionId, [new MealieDisconnected(connectionId, guardianId, time.GetUtcNow())], cancellationToken);
        }

        await store.MaskStreamAsync<MealieConnectionSnapshot>(
            connectionId.Value,
            cancellationToken,
            e => e.Data is MealieConnected connected && connected.ConnectedBy == guardianId);
    }

    private async Task DeleteAnchoredAsync(UserId childId, CancellationToken cancellationToken)
    {
        var anchor = childId.Value;
        IReadOnlyList<MealIndexDocument> mealDocs;
        IReadOnlyList<MealPlanIndexDocument> planDocs;
        IReadOnlyList<AiCredentialIndexDocument> credentialDocs;
        IReadOnlyList<AiSessionIndexDocument> sessionDocs;
        IReadOnlyList<GroupSharedMealPlanDocument> sharedDocs;
        IReadOnlyList<MealieConnectionIndexDocument> connectionDocs;

        await using (var session = store.QuerySession())
        {
            connectionDocs = await session.Query<MealieConnectionIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken);
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

        foreach (var doc in connectionDocs)
        {
            await store.DeleteStreamAsync<MealieConnectionSnapshot>(doc.Id, cancellationToken, s => s.Delete(doc));
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

        foreach (var doc in await session.Query<MealieConnectionIndexDocument>().Where(d => d.ChildId == anchor).ToListAsync(cancellationToken))
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
