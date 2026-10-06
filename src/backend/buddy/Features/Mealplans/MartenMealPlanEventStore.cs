using buddy.Common.Concurrency;
using buddy.Features.Groups;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

public sealed class MartenMealPlanEventStore(IMealplansStore store) : IMealPlanEventStore
{
    public async Task<IReadOnlyCollection<MealPlanEvent>> ReadAsync(MealPlanId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => MealPlanEvent.FromPayload(e.Data))];
    }

    public async Task<MealPlan?> FindSnapshotAsync(MealPlanId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<MealPlanSnapshot>(id.Value, cancellationToken);

        return snapshot?.MealPlan;
    }

    public async Task<IReadOnlyCollection<MealPlanEvent>> CreateAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            MealPlanCreated created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new meal plan stream must be MealPlanCreated."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal plan event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, payloads);
        session.Store(new MealPlanIndexDocument(id.Value, childId.Value));

        // Lazy creation can bundle a share into the same batch (e.g. sharing a family's very
        // first, still-empty plan) -- apply any document side effects for every event in the
        // batch, not just the first, the same way AppendAsync does.
        foreach (var @event in events)
        {
            ApplyGroupSharingDocumentEffects(session, id, @event);
        }

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal plan event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, payloads);

        foreach (var @event in events)
        {
            ApplyGroupSharingDocumentEffects(session, id, @event);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task RevertImportAsync(
        MealPlanId id, IReadOnlyCollection<MealPlanEvent> planEvents, IReadOnlyCollection<MealArchived> archivedMeals, CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();

        session.AppendTracked(id.Value, [.. planEvents.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal plan event."))]);

        foreach (var archived in archivedMeals)
        {
            session.AppendTracked(archived.Id.Value, [archived]);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task ImportAsync(
        MealPlanId id, IReadOnlyCollection<MealPlanEvent> planEvents, IReadOnlyCollection<IReadOnlyCollection<MealEvent>> newMeals, CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();

        foreach (var mealEvents in newMeals)
        {
            var created = mealEvents.FirstOrDefault() switch
            {
                MealCreated first => first,
                _ => throw new InvalidOperationException("The first event of a new meal stream must be MealCreated."),
            };

            session.StartTrackedStream(created.Id.Value, [.. mealEvents.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal event."))]);
            session.Store(new MealIndexDocument(created.Id.Value, created.ChildId.Value));
        }

        var payloads = planEvents
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal plan event."))
            .ToArray();

        if (planEvents.FirstOrDefault() is MealPlanCreated planCreated)
        {
            session.StartTrackedStream(id.Value, payloads);
            session.Store(new MealPlanIndexDocument(id.Value, planCreated.ChildId.Value));
        }
        else if (payloads.Length > 0)
        {
            session.AppendTracked(id.Value, payloads);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<MealPlanId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<MealPlanIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : new MealPlanId(doc.Id);
    }

    public async Task<GroupSharedMealPlanDocument?> FindGroupSharedAsync(GroupId groupId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GroupSharedMealPlanDocument>()
            .Where(d => d.GroupId == groupId.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // Id is the MealPlanId itself, so re-sharing with a different group is a plain upsert
    // (session.Store overwrites the row) -- see GroupSharedMealPlanDocument.
    private static void ApplyGroupSharingDocumentEffects(IDocumentSession session, MealPlanId id, MealPlanEvent @event)
    {
        switch (@event)
        {
            case MealPlanSharedWithGroup shared:
                session.Store(new GroupSharedMealPlanDocument(id.Value, shared.GroupId.Value, shared.AnchorChildId.Value));
                break;

            case MealPlanUnsharedFromGroup:
                session.Delete<GroupSharedMealPlanDocument>(id.Value);
                break;
        }
    }
}
