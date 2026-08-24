using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class GroupEventShapeTests
{
    private static readonly GroupId FixedGroupId = new(Guid.Parse("00000000-0000-0000-0000-000000000010"));
    private static readonly UserId FixedUserId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly UserId OtherUserId = new(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    private static readonly Guid FixedInviteId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedExpiry = new(2025, 1, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly ImmutableDictionary<GroupRole, CalendarRole> DefaultPolicy = ImmutableDictionary<GroupRole, CalendarRole>.Empty
        .Add(GroupRole.Owner, CalendarRole.Owner)
        .Add(GroupRole.Admin, CalendarRole.Contributor)
        .Add(GroupRole.Member, CalendarRole.Viewer);

    [Fact]
    public void GroupCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupCreated(FixedGroupId, FixedUserId, "Engineering", DefaultPolicy, FixedInstant),
        "Groups/GroupCreated.json");

    [Fact]
    public void GroupDeleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupDeleted(FixedGroupId, FixedUserId, FixedInstant),
        "Groups/GroupDeleted.json");

    [Fact]
    public void GroupMemberRoleGranted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupMemberRoleGranted(FixedGroupId, OtherUserId, GroupRole.Admin, FixedUserId, FixedInstant),
        "Groups/GroupMemberRoleGranted.json");

    [Fact]
    public void GroupMemberRoleRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupMemberRoleRevoked(FixedGroupId, OtherUserId, FixedUserId, FixedInstant),
        "Groups/GroupMemberRoleRevoked.json");

    [Fact]
    public void GroupCalendarPolicyUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupCalendarPolicyUpdated(FixedGroupId, DefaultPolicy, FixedUserId, FixedInstant),
        "Groups/GroupCalendarPolicyUpdated.json");

    [Fact]
    public void GroupInviteCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupInviteCreated(FixedGroupId, FixedInviteId, "invitee@buddy.test", GroupRole.Member, FixedUserId, "deadbeef", FixedExpiry, FixedInstant),
        "Groups/GroupInviteCreated.json");

    [Fact]
    public void GroupInviteAccepted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupInviteAccepted(FixedGroupId, FixedInviteId, OtherUserId, FixedInstant),
        "Groups/GroupInviteAccepted.json");

    [Fact]
    public void GroupInviteRevoked() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new GroupInviteRevoked(FixedGroupId, FixedInviteId, FixedUserId, FixedInstant),
        "Groups/GroupInviteRevoked.json");
}
