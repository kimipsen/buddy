using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public union MedicineSharingEvent(
    MedicineSharedWithGroup,
    MedicineUnsharedFromGroup
)
{
    public static MedicineSharingEvent FromPayload(object payload) => payload switch
    {
        MedicineSharedWithGroup e => e,
        MedicineUnsharedFromGroup e => e,
        _ => throw new ArgumentException($"Unknown medicine sharing event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        MedicineSharedWithGroup => nameof(MedicineSharedWithGroup),
        MedicineUnsharedFromGroup => nameof(MedicineUnsharedFromGroup),
    };
}

// Doubles as the stream's creation event (first event -> new MedicineSharing) and as a re-share
// (a later one just overwrites SharedWithGroupId) -- there's no separate "started" event, unlike
// MealPlanCreated, because nothing else ever touches this stream before a share happens.
public sealed record MedicineSharedWithGroup(MedicineSharingId Id, UserId ChildId, GroupId GroupId, UserId SharedBy, DateTimeOffset OccurredAt);

public sealed record MedicineUnsharedFromGroup(MedicineSharingId Id, GroupId GroupId, UserId UnsharedBy, DateTimeOffset OccurredAt);
