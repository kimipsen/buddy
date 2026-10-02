using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

using buddy.Common.Aggregates;
using buddy.Features.Groups;

namespace buddy.Features.Mealplans;

// No ChildId: a MealPlan is a family-wide singleton shared by every sibling (see
// MealFamilyResolution), not owned by the single child whose guardian happened to create it.
// SharedWithGroupId is additive, not an owner union like Calendar.Owner -- a MealPlan is always
// fundamentally family-owned; sharing with a group is an extra grant on top, never a replacement
// (see docs/backend/analysis/group-owned-mealplans.md).
//
// SlotTimes and Tokens back the iCal feed (see docs/backend/analysis/mealplan-ical-feed.md): a
// slot missing from SlotTimes falls back to MealSlotDefaultTimes at render time rather than being
// backfilled here, and Tokens mirrors Calendar.Tokens for the same anonymous-feed-by-token scheme.
public sealed record MealPlan(
    MealPlanId Id,
    ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment> Assignments,
    ImmutableDictionary<MealSlot, TimeOnly> SlotTimes,
    ImmutableDictionary<IcalTokenId, IcalTokenInfo> Tokens,
    GroupId? SharedWithGroupId = null)
{
    // Constant-time per candidate, mirroring Calendar.FindMatchingToken -- the caller supplies an
    // already-hashed value so the plaintext token is never compared or logged here.
    public IcalTokenId? FindMatchingToken(string submittedTokenHash)
    {
        var submittedBytes = Encoding.UTF8.GetBytes(submittedTokenHash);

        foreach (var (id, info) in Tokens)
        {
            if (CryptographicOperations.FixedTimeEquals(submittedBytes, Encoding.UTF8.GetBytes(info.Hash)))
            {
                return id;
            }
        }

        return null;
    }

    public static MealPlan? Rehydrate(IEnumerable<MealPlanEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static MealPlan Replay(IEnumerable<MealPlanEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so MealPlanSnapshotProjection
    // can drive the same logic one Marten-delivered event at a time instead of duplicating this
    // switch. Deliberately not named Apply/Create -- those names are a convention JasperFx's
    // projection source generator scans for on any type used as a projection document, and MealPlan
    // is that document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static MealPlan Start(MealPlanEvent @event) => @event switch
    {
        MealPlanCreated created => new MealPlan(
            created.Id,
            ImmutableDictionary<(DateOnly, MealSlot), MealPlanAssignment>.Empty,
            ImmutableDictionary<MealSlot, TimeOnly>.Empty,
            ImmutableDictionary<IcalTokenId, IcalTokenInfo>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(MealPlan), @event.EventType)
    };

    public static MealPlan Advance(MealPlan plan, MealPlanEvent @event) => @event switch
    {
        // Sparse dictionary: only slots a guardian actually filled hold a key, so a plan
        // for a year is one small stream, not one entry per possible date/slot.
        MealAssignedToSlot assigned => plan with
        {
            Assignments = plan.Assignments.SetItem((assigned.Date, assigned.Slot), assigned.After)
        },
        MealSlotCleared cleared => plan with
        {
            Assignments = plan.Assignments.Remove((cleared.Date, cleared.Slot))
        },
        // At most one group at a time -- sharing with a second group simply overwrites
        // the first (see "Remaining open questions" in group-owned-mealplans.md).
        MealPlanSharedWithGroup shared => plan with { SharedWithGroupId = shared.GroupId },
        MealPlanUnsharedFromGroup => plan with { SharedWithGroupId = null },
        MealPlanSlotTimeSet timeSet => plan with { SlotTimes = plan.SlotTimes.SetItem(timeSet.Slot, timeSet.Time) },
        MealPlanIcalTokenIssued issued => plan with { Tokens = plan.Tokens.SetItem(issued.TokenId, new IcalTokenInfo(issued.Hash, issued.OccurredAt)) },
        MealPlanIcalTokenRevoked revoked => plan with { Tokens = plan.Tokens.Remove(revoked.TokenId) },
        MealPlanCreated => throw EventReplay.AlreadyStarted(nameof(MealPlan), @event.EventType)
    };
}
