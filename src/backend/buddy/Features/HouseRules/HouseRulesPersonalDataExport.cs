using buddy.Common.Erasure;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// The "houseRules" section: the personal rule book of the subject (when the subject is a child) and
// of each of their children, plus the book of every household group they belong to. Acknowledgement
// status is limited to the subject and their children.
public sealed class HouseRulesPersonalDataExporter(
    IRuleBookEventStore books,
    IGroupEventStore groups,
    IGuardianLinkEventStore guardians) : IPersonalDataExporter
{
    public Type Store => typeof(IHouseRulesStore);

    public string Section => "houseRules";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        HashSet<UserId> people = [subject.UserId, .. subject.Children];
        List<ExportedRuleBook> exported = [];

        foreach (var childId in people)
        {
            var scope = RuleBookScope.ForChild(childId);

            if (await RuleBooks.FindSnapshotAsync(books, scope, cancellationToken) is { } book)
            {
                exported.Add(ExportedRuleBook.From(scope, book, [childId]));
            }
        }

        foreach (var membership in await groups.ListForUserAsync(subject.UserId, cancellationToken))
        {
            var scope = RuleBookScope.ForGroup(new GroupId(membership.GroupId));

            if (await RuleBooks.FindSnapshotAsync(books, scope, cancellationToken) is { } book)
            {
                var children = await HouseRulesAuthorization.ListChildrenInScopeAsync(scope, groups, guardians, cancellationToken);
                exported.Add(ExportedRuleBook.From(scope, book, [.. children.Where(people.Contains)]));
            }
        }

        return exported;
    }
}

public sealed record ExportedRuleBook(RuleBookScopeKind ScopeKind, Guid ScopeId, IReadOnlyList<RuleResponse> Rules)
{
    public static ExportedRuleBook From(RuleBookScope scope, RuleBook book, IReadOnlyList<UserId> children) =>
        new(scope.Kind, scope.Id, [.. book.Rules.Select(rule => RuleResponse.From(book, rule, children))]);
}
