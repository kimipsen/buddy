using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

// Aggregate-only, never inside a persisted event: the two creation events carry the owner as a
// plain UserId/GroupId, the same split CalendarCreated/CalendarCreatedForGroup use (see
// docs/backend/analysis/week-plan-print-templates.md, Question 2). It is serialized only as part of
// the snapshot, through PrintTemplateOwnerJsonConverter.
public union PrintTemplateOwner(PrintTemplateOwner.User, PrintTemplateOwner.Group)
{
    public sealed record User(UserId Value);
    public sealed record Group(GroupId Value);
}
