using buddy.Common.Erasure;
using buddy.Features.Groups;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.HouseRules;

// A child's personal rule book is about them alone: erasing the child deletes it. Household books
// belong to the group and stay while the group does; the erased child's acknowledgements in them are
// keyed by a UserId, a pseudonym once the user is erased, and the child is no longer a member, so
// they drop out of every status list. Rules a guardian wrote stay with the child or household they
// were written for -- AddedBy/EditedBy are pseudonyms too, as CreatedBy is on calendar items.
//
// A group the erasure left with nobody in it is deleted by GroupsPersonalDataEraser (which runs
// first); its rule book goes with it here, as its calendars go there. See gdpr-data-protection.md.
public sealed class HouseRulesPersonalDataEraser(IHouseRulesStore store, IGroupEventStore groups) : IPersonalDataEraser
{
    public Type Store => typeof(IHouseRulesStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) =>
        DeleteBooksOfDeletedGroupsAsync(cancellationToken);

    public async Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken)
    {
        await store.DeleteStreamAsync<RuleBookSnapshot>(RuleBookId.ForChild(child.UserId).Value, cancellationToken);
        await DeleteBooksOfDeletedGroupsAsync(cancellationToken);
    }

    // The deleted group's membership documents are gone by now, so the books are found from this
    // side. A family installation holds a handful of household books, so checking each is cheap.
    private async Task DeleteBooksOfDeletedGroupsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> groupBooks;

        await using (var session = store.QuerySession())
        {
            groupBooks = await session.Query<RuleBookSnapshot>()
                .Where(s => s.RuleBook.ScopeKind == RuleBookScopeKind.Group)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (var bookId in groupBooks)
        {
            if (await groups.FindSnapshotAsync(new GroupId(bookId), cancellationToken) is null or { IsDeleted: true })
            {
                await store.DeleteStreamAsync<RuleBookSnapshot>(bookId, cancellationToken);
            }
        }
    }
}
