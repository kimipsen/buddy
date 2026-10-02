namespace buddy.Features.PrintTemplates;

// Answers "which templates can I use" without scanning streams: written in the same session as the
// creation, rename and delete appends. Exactly one of OwnerUserId/OwnerGroupId is set.
public sealed record PrintTemplateIndexDocument(Guid Id, Guid? OwnerUserId, Guid? OwnerGroupId, string Name, bool IsDeleted)
{
    public static PrintTemplateIndexDocument From(PrintTemplate template) => template.Owner switch
    {
        PrintTemplateOwner.User(var userId) => new(template.Id.Value, userId.Value, null, template.Name, template.IsDeleted),
        PrintTemplateOwner.Group(var groupId) => new(template.Id.Value, null, groupId.Value, template.Name, template.IsDeleted),
    };
}
