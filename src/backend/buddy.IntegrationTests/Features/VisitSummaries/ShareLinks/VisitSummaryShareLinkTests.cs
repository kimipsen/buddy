using buddy.Common.Security;
using buddy.Features.Users;
using buddy.Features.VisitSummaries;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.SleepDiaries;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.VisitSummaries.VisitSummaryTestHelpers;

namespace buddy.IntegrationTests.Features.VisitSummaries.ShareLinks;

[Collection(BuddyApiCollection.Name)]
public sealed class VisitSummaryShareLinkTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateVisitSummaryShareLink")]
    [CoversEndpoint("GetSharedVisitSummary")]
    public async Task A_share_link_shows_an_anonymous_reader_the_live_report_for_the_fixed_period()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil", "Holm");
        var summary = await CreateAsync(fixture, token, child.Id);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var link = await CreateShareLinkAsync(fixture, token, child.Id, summary.Id, expiresAt);
        var shared = await GetSharedAsync(fixture, link.Token);

        Assert.False(string.IsNullOrWhiteSpace(link.Token));
        Assert.Equal("Emil", shared!.Report.ChildGivenName);
        Assert.Equal((From, To), (shared.Report.From, shared.Report.To));
        Assert.Empty(shared.Report.Sleep!.Entries);
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), shared.ExpiresAt!.Value.ToUnixTimeSeconds());
        Assert.Equal(link.Id, Assert.Single((await ListAsync(fixture, token, child.Id))!.Single().ShareLinks).Id);

        // Current data on every read, not a copy taken when the link was made.
        await SleepDiaryTestHelpers.LogAsync(fixture, token, child.Id, From, SleepDiaryTestHelpers.FullNight());
        Assert.Single((await GetSharedAsync(fixture, link.Token))!.Report.Sleep!.Entries);
    }

    [Fact]
    [CoversEndpoint("RevokeVisitSummaryShareLink")]
    public async Task A_revoked_link_stops_working_and_revoking_again_is_a_no_op()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);
        var revoked = await CreateShareLinkAsync(fixture, token, child.Id, summary.Id);
        var kept = await CreateShareLinkAsync(fixture, token, child.Id, summary.Id);

        await RevokeShareLinkAsync(fixture, token, child.Id, summary.Id, revoked.Id);
        await RevokeShareLinkAsync(fixture, token, child.Id, summary.Id, revoked.Id);

        await GetSharedAsync(fixture, revoked.Token, expectedStatus: 404);
        await GetSharedAsync(fixture, kept.Token);
        Assert.Equal(kept.Id, Assert.Single((await GetAsync(fixture, token, child.Id, summary.Id))!.Summary.ShareLinks).Id);
        await RevokeShareLinkAsync(fixture, token, child.Id, summary.Id, Guid.CreateVersion7(), expectedStatus: 404);
    }

    [Fact]
    public async Task Deleting_the_summary_revokes_every_link()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);
        var link = await CreateShareLinkAsync(fixture, token, child.Id, summary.Id);

        await DeleteAsync(fixture, token, child.Id, summary.Id);

        await GetSharedAsync(fixture, link.Token, expectedStatus: 404);
        await CreateShareLinkRawAsync(fixture, token, child.Id, summary.Id, null, 404);
    }

    [Fact]
    public async Task A_summary_holds_at_most_five_live_links()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);
        var links = new List<VisitSummaryShareLinkDto>();

        for (var i = 0; i < VisitSummaryRules.MaxLiveLinks; i++)
        {
            links.Add(await CreateShareLinkAsync(fixture, token, child.Id, summary.Id));
        }

        await CreateShareLinkRawAsync(fixture, token, child.Id, summary.Id, null, 400);
        await RevokeShareLinkAsync(fixture, token, child.Id, summary.Id, links[0].Id);
        await CreateShareLinkAsync(fixture, token, child.Id, summary.Id);
    }

    [Fact]
    public async Task A_past_or_too_distant_expiry_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);

        await CreateShareLinkRawAsync(fixture, token, child.Id, summary.Id, DateTimeOffset.UtcNow.AddMinutes(-1), 400);
        await CreateShareLinkRawAsync(fixture, token, child.Id, summary.Id, DateTimeOffset.UtcNow.AddDays(VisitSummaryRules.MaxShareLinkExpiryDays + 1), 400);
    }

    [Fact]
    public async Task Unknown_and_expired_links_get_not_found()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var summary = await CreateAsync(fixture, token, child.Id);

        // The API refuses a past expiry, so the expired link is appended straight to the stream.
        var store = fixture.Host.Services.GetRequiredService<IVisitSummaryEventStore>();
        var (plaintext, hash) = ShareSecret.Generate();
        var id = new VisitSummaryId(summary.Id);
        await store.AppendAsync(
            id,
            [new VisitSummaryShareLinkCreated(id, VisitSummaryShareLinkId.New(), hash, DateTimeOffset.UtcNow.AddMinutes(-1), new UserId(guardianId), DateTimeOffset.UtcNow.AddDays(-1))],
            CancellationToken.None);

        await GetSharedAsync(fixture, plaintext, expectedStatus: 404);
        await GetSharedAsync(fixture, "not-a-real-token", expectedStatus: 404);
        Assert.Empty((await GetAsync(fixture, token, child.Id, summary.Id))!.Summary.ShareLinks);
    }
}
