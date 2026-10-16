using buddy.Features.VisitSummaries;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.VisitSummaries.VisitSummaryTestHelpers;

namespace buddy.IntegrationTests.Features.VisitSummaries.Summaries;

[Collection(BuddyApiCollection.Name)]
public sealed class VisitSummaryTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateVisitSummary")]
    [CoversEndpoint("ListVisitSummaries")]
    public async Task A_guardian_creates_summaries_and_lists_them_newest_visit_first()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        var march = await CreateAsync(fixture, token, child.Id, Body(
            title: "  March follow-up  ", sections: ["Sleep", "Medicine", "Sleep"], note: "  Ask about lunch  "));
        var april = await CreateAsync(fixture, token, child.Id, Body(title: "April", visitDate: new DateOnly(2026, 5, 1)));

        Assert.Equal("March follow-up", march.Title);
        Assert.Equal("Ask about lunch", march.Note);
        Assert.Equal(["Medicine", "Sleep"], march.Sections);
        Assert.Equal(From, march.From);
        Assert.Equal(To, march.To);
        Assert.Equal(guardianId, march.CreatedBy);
        Assert.Empty(march.ShareLinks);

        var listed = await ListAsync(fixture, token, child.Id);
        Assert.Equal([april.Id, march.Id], listed!.Select(s => s.Id));
    }

    [Fact]
    [CoversEndpoint("UpdateVisitSummary")]
    public async Task Updating_overwrites_the_details_and_an_unchanged_save_appends_nothing()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);

        var edited = Body(title: "Changed", sections: ["Feelings"], note: "", from: From.AddDays(7), to: To.AddDays(7));
        var updated = (await UpdateAsync(fixture, token, child.Id, summary.Id, edited)).ReadAsJson<VisitSummaryDto>()!;
        await UpdateAsync(fixture, token, child.Id, summary.Id, edited);

        Assert.Equal("Changed", updated.Title);
        Assert.Equal(["Feelings"], updated.Sections);
        Assert.Equal("", updated.Note);
        Assert.Equal(From.AddDays(7), updated.From);

        var store = fixture.Host.Services.GetRequiredService<IVisitSummaryEventStore>();
        var events = await store.ReadAsync(new VisitSummaryId(summary.Id), CancellationToken.None);
        Assert.Equal(["VisitSummaryCreated", "VisitSummaryUpdated"], events.Select(e => e.EventType));
    }

    [Fact]
    [CoversEndpoint("DeleteVisitSummary")]
    public async Task A_deleted_summary_leaves_the_list_and_deleting_again_is_a_no_op()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var kept = await CreateAsync(fixture, token, child.Id);
        var deleted = await CreateAsync(fixture, token, child.Id);

        await DeleteAsync(fixture, token, child.Id, deleted.Id);
        await DeleteAsync(fixture, token, child.Id, deleted.Id);

        Assert.Equal(kept.Id, Assert.Single((await ListAsync(fixture, token, child.Id))!).Id);
        await GetAsync(fixture, token, child.Id, deleted.Id, expectedStatus: 404);
        await UpdateAsync(fixture, token, child.Id, deleted.Id, Body(), expectedStatus: 404);
    }

    [Theory]
    [InlineData("", "Medicine", 0, 27)]
    [InlineData("Visit", "", 0, 27)]
    [InlineData("Visit", "Medicine", 0, 92)]
    [InlineData("Visit", "Medicine", 10, 9)]
    public async Task Invalid_details_are_rejected(string title, string section, int fromOffset, int toOffset)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        string[] sections = section == "" ? [] : [section];

        var body = Body(title: title, sections: sections, from: From.AddDays(fromOffset), to: From.AddDays(toOffset));

        await CreateRawAsync(fixture, token, child.Id, body, 400);
    }

    [Fact]
    public async Task A_92_day_period_and_a_4000_character_note_are_accepted_but_no_more()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        var summary = await CreateAsync(fixture, token, child.Id, Body(from: From, to: From.AddDays(91), note: new string('x', VisitSummaryRules.MaxNoteLength)));

        await UpdateAsync(fixture, token, child.Id, summary.Id, Body(note: new string('x', VisitSummaryRules.MaxNoteLength + 1)), expectedStatus: 400);
        await UpdateAsync(fixture, token, child.Id, summary.Id, Body(title: new string('t', VisitSummaryRules.MaxTitleLength + 1)), expectedStatus: 400);
    }

    [Fact]
    public async Task A_co_guardian_manages_the_summaries_too()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var summary = await CreateAsync(fixture, family.FirstToken, family.Child.Id);

        await UpdateAsync(fixture, family.SecondToken, family.Child.Id, summary.Id, Body(title: "Edited by the other parent"));

        Assert.Equal("Edited by the other parent", Assert.Single((await ListAsync(fixture, family.FirstToken, family.Child.Id))!).Title);
    }

    [Fact]
    public async Task A_stranger_the_child_and_another_childs_route_all_get_not_found()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Sam");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var summary = await CreateAsync(fixture, token, child.Id);

        foreach (var caller in new[] { strangerToken, childToken })
        {
            await CreateRawAsync(fixture, caller, child.Id, Body(), 404);
            await ListAsync(fixture, caller, child.Id, expectedStatus: 404);
            await GetAsync(fixture, caller, child.Id, summary.Id, expectedStatus: 404);
            await UpdateAsync(fixture, caller, child.Id, summary.Id, Body(), expectedStatus: 404);
            await DeleteAsync(fixture, caller, child.Id, summary.Id, expectedStatus: 404);
            await CreateShareLinkRawAsync(fixture, caller, child.Id, summary.Id, null, 404);
        }

        // The guardian's own summary, addressed through their other child.
        await GetAsync(fixture, token, sibling.Id, summary.Id, expectedStatus: 404);
        await DeleteAsync(fixture, token, sibling.Id, summary.Id, expectedStatus: 404);
        await GetAsync(fixture, token, child.Id, Guid.CreateVersion7(), expectedStatus: 404);
    }
}
