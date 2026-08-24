using buddy.Features.Calendars;
using buddy.Features.Groups;

namespace buddy.IntegrationTests.Features.Groups;

// Shared response shapes for the Groups endpoint tests, matching the wire format produced by
// GroupResponse.FromGroup (Features/Groups/GetGroup/GetGroup.Endpoint.cs): strongly-typed ids
// serialize as a raw Guid (StronglyTypedIdJsonConverterFactory), and CalendarPermissionPolicy's
// enum-keyed dictionary serializes with the enum member name as the JSON key.
internal sealed record GroupResponseDto(Guid Id, string Name, IReadOnlyCollection<GroupMemberDto> Members, Dictionary<GroupRole, CalendarRole> CalendarPermissionPolicy);

internal sealed record GroupMemberDto(Guid UserId, GroupRole Role);

internal sealed record GroupSummaryDto(Guid Id, string Name, GroupRole Role);

internal sealed record GroupInviteResponseDto(Guid Id, string Email, GroupRole Role, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt);

internal sealed record GroupInvitePreviewResponseDto(string GroupName);
