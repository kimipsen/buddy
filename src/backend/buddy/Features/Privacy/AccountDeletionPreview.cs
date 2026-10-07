namespace buddy.Features.Privacy;

// GET /users/me/deletion-preview: what DELETE /users/me would also take with it. ChildrenErased have
// no other guardian; GroupsHandedOver pass to NewOwner; GroupsDeleted have nobody left to inherit.
public sealed record AccountDeletionPreview(
    IReadOnlyCollection<PreviewPerson> ChildrenErased,
    IReadOnlyCollection<GroupHandover> GroupsHandedOver,
    IReadOnlyCollection<PreviewGroup> GroupsDeleted);

public sealed record PreviewPerson(Guid Id, string GivenName, string FamilyName);

public sealed record PreviewGroup(Guid Id, string Name);

public sealed record GroupHandover(Guid Id, string Name, PreviewPerson NewOwner);
