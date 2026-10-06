using System.Collections.Immutable;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public union MealPlanEvent(
    MealPlanCreated,
    MealAssignedToSlot,
    MealSlotCleared,
    MealPlanSharedWithGroup,
    MealPlanUnsharedFromGroup,
    MealPlanSlotTimeSet,
    MealPlanIcalTokenIssued,
    MealPlanIcalTokenRevoked,
    MealPlanEntriesImported,
    MealPlanImportReverted
)
{
    public static MealPlanEvent FromPayload(object payload) => payload switch
    {
        MealPlanCreated e => e,
        MealAssignedToSlot e => e,
        MealSlotCleared e => e,
        MealPlanSharedWithGroup e => e,
        MealPlanUnsharedFromGroup e => e,
        MealPlanSlotTimeSet e => e,
        MealPlanIcalTokenIssued e => e,
        MealPlanIcalTokenRevoked e => e,
        MealPlanEntriesImported e => e,
        MealPlanImportReverted e => e,
        _ => throw new ArgumentException($"Unknown meal plan event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        MealPlanCreated => nameof(MealPlanCreated),
        MealAssignedToSlot => nameof(MealAssignedToSlot),
        MealSlotCleared => nameof(MealSlotCleared),
        MealPlanSharedWithGroup => nameof(MealPlanSharedWithGroup),
        MealPlanUnsharedFromGroup => nameof(MealPlanUnsharedFromGroup),
        MealPlanSlotTimeSet => nameof(MealPlanSlotTimeSet),
        MealPlanIcalTokenIssued => nameof(MealPlanIcalTokenIssued),
        MealPlanIcalTokenRevoked => nameof(MealPlanIcalTokenRevoked),
        MealPlanEntriesImported => nameof(MealPlanEntriesImported),
        MealPlanImportReverted => nameof(MealPlanImportReverted),
    };
}

// Appended lazily by the first AssignMealToSlot call for a family with no MealPlan stream yet
// (see MealFamilyResolution), bundled into the same CreateAsync as that first MealAssignedToSlot
// -- not provisioned as part of CreateChild (Mealplans stays decoupled from Guardians the same way
// Medicines never hooks into child creation either). ChildId records which child the creating
// guardian was acting on behalf of, needed by MartenMealPlanEventStore.CreateAsync to seed the
// plan's first index row, but not projected onto the MealPlan aggregate itself -- the whole family
// shares this one stream, so "whose plan is this" isn't aggregate state. Two guardians assigning a
// family's very first slot at the same instant could race into two streams; accepted for v1 as the
// same class of last-write-wins tradeoff event sourcing already accepts elsewhere in this codebase
// (see docs/backend/analysis/mealplans.md).
public sealed record MealPlanCreated(MealPlanId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Sets or overwrites the slot. The assignment replaced isn't carried: it is already in the stream,
// and nothing reads it.
public sealed record MealAssignedToSlot(MealPlanId Id, DateOnly Date, MealSlot Slot, MealPlanAssignment Assignment, DateTimeOffset OccurredAt);

public sealed record MealSlotCleared(MealPlanId Id, DateOnly Date, MealSlot Slot, MealPlanAssignment Before, UserId ModifiedBy, DateTimeOffset OccurredAt);

// AnchorChildId is carried here rather than resolved later -- it is exactly the ChildId the
// sharing guardian was already authorized against, and it's what lets a group-keyed request
// resolve back into the existing MealFamilyResolution machinery unchanged (see
// docs/backend/analysis/group-owned-mealplans.md). Additive, not a modification to
// MealPlanCreated -- MealplanAuthorization's ChildId/callerId resolution is untouched by this.
public sealed record MealPlanSharedWithGroup(MealPlanId Id, GroupId GroupId, UserId AnchorChildId, UserId SharedBy, DateTimeOffset OccurredAt);

public sealed record MealPlanUnsharedFromGroup(MealPlanId Id, GroupId GroupId, UserId UnsharedBy, DateTimeOffset OccurredAt);

// One slot per event, the same granularity MealAssignedToSlot already uses for one date/slot at a
// time -- see docs/backend/analysis/mealplan-ical-feed.md. A slot with no MealPlanSlotTimeSet in
// its stream falls back to MealSlotDefaultTimes, so this is never appended on plan creation.
public sealed record MealPlanSlotTimeSet(MealPlanId Id, MealSlot Slot, TimeOnly Time, UserId ModifiedBy, DateTimeOffset OccurredAt);

// Mirrors Calendars' IcalTokenIssued/IcalTokenRevoked (CalendarEvents.cs) -- see
// docs/backend/analysis/mealplan-ical-feed.md for why this is a feature-local duplicate rather than
// a shared type.
public sealed record MealPlanIcalTokenIssued(MealPlanId Id, IcalTokenId TokenId, string Hash, UserId IssuedBy, DateTimeOffset OccurredAt);

public sealed record MealPlanIcalTokenRevoked(MealPlanId Id, IcalTokenId TokenId, UserId RevokedBy, DateTimeOffset OccurredAt);

// One event per import commit, carrying every assignment it wrote -- not one MealAssignedToSlot
// per day -- so the stream records "these days came from an import" and RevertMealPlanImport can
// find them again by ImportId (see docs/backend/analysis/mealplan-import.md, Question 5). Folded
// exactly like that many MealAssignedToSlot. Entries never include a slot that was already
// occupied when the import ran: an import never overwrites. CreatedMealIds are the meals this
// commit created, so a revert can archive the ones nothing else uses.
public sealed record MealPlanEntriesImported(
    MealPlanId Id,
    MealPlanImportId ImportId,
    string Format,
    ImmutableArray<ImportedMealPlanEntry> Entries,
    ImmutableArray<MealId> CreatedMealIds,
    UserId ImportedBy,
    DateTimeOffset OccurredAt);

public sealed record ImportedMealPlanEntry(DateOnly Date, MealSlot Slot, MealPlanAssignment Assignment);

// Appended after the MealSlotCleared events of a revert. Marks the import as undone so a second
// revert is a no-op and ListMealPlanImports can show it; it changes no aggregate state itself.
public sealed record MealPlanImportReverted(MealPlanId Id, MealPlanImportId ImportId, UserId RevertedBy, DateTimeOffset OccurredAt);
