using buddy.Common.Security;
using buddy.Features.Feelings;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.Feelings.FeelingsTestHelpers;

namespace buddy.IntegrationTests.Features.Feelings.ShareLinks;

// A doctor's read-only link, copied from the sleep diary's (Question 8).
[Collection(BuddyApiCollection.Name)]
public sealed class FeelingsShareLinkTests(BuddyApiFixture fixture)
{
    private const string Copenhagen = "Europe/Copenhagen";

    [Fact]
    [CoversEndpoint("CreateFeelingsShareLink")]
    [CoversEndpoint("GetSharedFeelings")]
    public async Task A_share_link_lets_an_anonymous_reader_see_the_check_ins_notes_included()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil", "Hansen");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var day = new DateOnly(2026, 3, 2);
        await LogAsync(fixture, token, child.Id, new { Zone = "Red", Note = "Meltdown at pickup", Date = day, Time = "15:10" });
        await LogAsync(fixture, token, child.Id, new { Zone = "Green", Date = day.AddDays(30), Time = "08:00" });
        await CheckInAsync(fixture, childToken, new { Zone = "Yellow" });
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var link = await CreateShareLinkAsync(fixture, token, child.Id, expiresAt);

        Assert.False(string.IsNullOrWhiteSpace(link!.Token));

        var shared = await GetSharedAsync(fixture, link.Token, $"?from={day:yyyy-MM-dd}&to={day.AddDays(13):yyyy-MM-dd}");

        Assert.Equal("Emil", shared!.ChildGivenName);
        Assert.Equal("Hansen", shared.ChildFamilyName);
        Assert.Equal(day, shared.From);
        Assert.Equal(day.AddDays(13), shared.To);
        var checkIn = Assert.Single(shared.CheckIns);
        Assert.Equal("Red", checkIn.Zone);
        Assert.Equal("Meltdown at pickup", checkIn.Note);
        Assert.False(checkIn.IsSelfReport);
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), shared.ExpiresAt!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Without_a_range_the_shared_view_covers_the_last_14_days_on_the_childs_clock_and_reflects_new_check_ins()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        await SetChildTimeZoneAsync(fixture, token, child.Id, Copenhagen);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (today, _) = LocalNow(Copenhagen);
        var link = await CreateShareLinkAsync(fixture, token, child.Id);

        var empty = await GetSharedAsync(fixture, link!.Token);
        Assert.Equal(today.AddDays(-13), empty!.From);
        Assert.Equal(today, empty.To);
        Assert.Empty(empty.CheckIns);
        Assert.Null(empty.ExpiresAt);

        await LogAsync(fixture, token, child.Id, new { Zone = "Blue", Date = today.AddDays(-14), Time = "08:00" });
        await CheckInAsync(fixture, childToken, new { Zone = "Green" });

        var shared = await GetSharedAsync(fixture, link.Token);
        Assert.True(Assert.Single(shared!.CheckIns).IsSelfReport);
    }

    [Fact]
    public async Task An_unknown_token_gets_not_found_and_a_bad_range_gets_bad_request()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var link = await CreateShareLinkAsync(fixture, token, child.Id);
        var day = new DateOnly(2026, 3, 2);

        await GetSharedAsync(fixture, "not-a-real-token", expectedStatus: 404);
        await GetSharedAsync(fixture, link!.Token, $"?from={day:yyyy-MM-dd}&to={day.AddDays(-1):yyyy-MM-dd}", expectedStatus: 400);
        await GetSharedAsync(fixture, link.Token, $"?from={day:yyyy-MM-dd}&to={day.AddDays(FeelingsRules.MaxRangeDays + 1):yyyy-MM-dd}", expectedStatus: 400);
        Assert.Equal(day.AddDays(13), (await GetSharedAsync(fixture, link.Token, $"?from={day:yyyy-MM-dd}"))!.To);
    }

    [Fact]
    [CoversEndpoint("ListFeelingsShareLinks")]
    [CoversEndpoint("RevokeFeelingsShareLink")]
    public async Task A_revoked_link_stops_working_and_leaves_the_list()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var first = await CreateShareLinkAsync(fixture, token, child.Id);
        var second = await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddDays(7));

        Assert.Equal([second!.Id, first!.Id], (await ListShareLinksAsync(fixture, token, child.Id))!.Select(l => l.Id));

        await RevokeShareLinkAsync(fixture, token, child.Id, first.Id);
        await RevokeShareLinkAsync(fixture, token, child.Id, first.Id);

        await GetSharedAsync(fixture, first.Token, expectedStatus: 404);
        await GetSharedAsync(fixture, second.Token);
        Assert.Equal(second.Id, Assert.Single((await ListShareLinksAsync(fixture, token, child.Id))!).Id);
    }

    [Fact]
    public async Task An_expired_link_gets_not_found_and_leaves_the_list()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");

        // The API refuses a past expiry, so the expired link is written straight to the store.
        var store = fixture.Host.Services.GetRequiredService<IFeelingsShareTokenEventStore>();
        var (plaintext, hash) = ShareSecret.Generate();
        var id = FeelingsShareTokenId.New();
        await store.CreateAsync(
            id,
            [new FeelingsShareTokenCreated(id, new UserId(child.Id), hash, new UserId(guardianId), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(-1))],
            CancellationToken.None);

        await GetSharedAsync(fixture, plaintext, expectedStatus: 404);
        Assert.Empty((await ListShareLinksAsync(fixture, token, child.Id))!);
    }

    [Fact]
    public async Task A_past_or_too_distant_expiry_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");

        await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddMinutes(-1), expectedStatus: 400);
        await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddDays(FeelingsRules.MaxShareLinkExpiryDays + 1), expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_gets_forbidden_and_a_stranger_not_found_on_share_links()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var link = await CreateShareLinkAsync(fixture, token, child.Id);

        await CreateShareLinkAsync(fixture, childToken, child.Id, expectedStatus: 403);
        await ListShareLinksAsync(fixture, childToken, child.Id, expectedStatus: 403);
        await RevokeShareLinkAsync(fixture, childToken, child.Id, link!.Id, expectedStatus: 403);

        await CreateShareLinkAsync(fixture, strangerToken, child.Id, expectedStatus: 404);
        await ListShareLinksAsync(fixture, strangerToken, child.Id, expectedStatus: 404);
        await RevokeShareLinkAsync(fixture, strangerToken, child.Id, link.Id, expectedStatus: 404);

        await GetSharedAsync(fixture, link.Token);
    }

    [Fact]
    public async Task A_link_cannot_be_revoked_through_another_child()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var emil = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Emil");
        var ida = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Ida");
        var link = await CreateShareLinkAsync(fixture, token, emil.Id);

        await RevokeShareLinkAsync(fixture, token, ida.Id, link!.Id, expectedStatus: 404);
        await RevokeShareLinkAsync(fixture, token, emil.Id, Guid.CreateVersion7(), expectedStatus: 404);

        await GetSharedAsync(fixture, link.Token);
    }

    [Fact]
    public async Task A_live_link_keeps_working_after_its_creators_guardian_link_is_revoked()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var link = await CreateShareLinkAsync(fixture, family.SecondToken, family.Child.Id);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await GetSharedAsync(fixture, link!.Token);
        Assert.Single((await ListShareLinksAsync(fixture, family.FirstToken, family.Child.Id))!);
    }
}
