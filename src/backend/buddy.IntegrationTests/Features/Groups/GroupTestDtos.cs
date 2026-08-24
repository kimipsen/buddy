using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Medicines;
using buddy.Features.Mealplans;

namespace buddy.IntegrationTests.Features.Groups;

// Shared response shapes for the Groups endpoint tests, matching the wire format produced by
// GroupResponse.FromGroup (Features/Groups/GetGroup/GetGroup.Endpoint.cs): strongly-typed ids
// serialize as a raw Guid (StronglyTypedIdJsonConverterFactory), and CalendarPermissionPolicy's/
// MealplanPermissionPolicy's/MedicinePermissionPolicy's enum-keyed dictionaries serialize with
// the enum member name as the JSON key.
internal sealed record GroupResponseDto(
    Guid Id,
    string Name,
    IReadOnlyCollection<GroupMemberDto> Members,
    Dictionary<GroupRole, CalendarRole> CalendarPermissionPolicy,
    Dictionary<GroupRole, MealplanAccessTier> MealplanPermissionPolicy,
    Dictionary<GroupRole, MedicineAccessTier> MedicinePermissionPolicy);

internal sealed record GroupMemberDto(Guid UserId, GroupRole Role);

internal sealed record GroupSummaryDto(Guid Id, string Name, GroupRole Role);

internal sealed record GroupInviteResponseDto(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt);

internal sealed record GroupInvitePreviewResponseDto(string GroupName);
