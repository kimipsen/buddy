using buddy.Features.Users;

namespace buddy.Features.Groups;

public interface IGroupEventStore
{
    Task<IReadOnlyCollection<GroupEvent>> ReadAsync(GroupId groupId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GroupEvent>> CreateAsync(GroupId groupId, IReadOnlyCollection<GroupEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(GroupId groupId, IReadOnlyCollection<GroupEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GroupMembershipDocument>> ListForUserAsync(UserId userId, CancellationToken cancellationToken);
}
