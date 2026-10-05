using buddy.Features.SleepDiaries;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.SleepDiaries.SleepDiaryTestHelpers;

namespace buddy.IntegrationTests.Features.SleepDiaries.ShareLinks;

[Collection(BuddyApiCollection.Name)]
public sealed class SleepDiaryShareLinkTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateSleepDiaryShareLink")]
    [CoversEndpoint("GetSharedSleepDiary")]
    public async Task A_share_link_lets_an_anonymous_reader_see_the_diary()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex", "Anderson");
        await LogAsync(fixture, token, child.Id, Monday, FullNight());
        await LogAsync(fixture, token, child.Id, Monday.AddDays(30), FullNight());
        await UpdateNotesAsync(fixture, token, child.Id, "Blackout curtains");
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var link = await CreateShareLinkAsync(fixture, token, child.Id, expiresAt);

        Assert.NotNull(link);
        Assert.False(string.IsNullOrWhiteSpace(link.Token));

        var shared = await GetSharedAsync(fixture, link.Token, $"?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(13):yyyy-MM-dd}");

        Assert.NotNull(shared);
        Assert.Equal("Alex", shared.ChildGivenName);
        Assert.Equal("Anderson", shared.ChildFamilyName);
        Assert.Equal(Monday, shared.From);
        Assert.Equal(Monday.AddDays(13), shared.To);
        Assert.Equal("Blackout curtains", shared.SleepHygieneNotes);
        Assert.Equal(Monday, Assert.Single(shared.Entries).Date);
        Assert.NotNull(shared.ExpiresAt);
        Assert.Equal(expiresAt.ToUnixTimeSeconds(), shared.ExpiresAt.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Without_a_range_the_shared_view_covers_the_last_14_days()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await LogAsync(fixture, token, child.Id, today, FullNight());
        await LogAsync(fixture, token, child.Id, today.AddDays(-14), FullNight());
        var link = await CreateShareLinkAsync(fixture, token, child.Id);

        var shared = await GetSharedAsync(fixture, link!.Token);

        Assert.Equal(today.AddDays(-13), shared!.From);
        Assert.Equal(today, shared.To);
        Assert.Equal(today, Assert.Single(shared.Entries).Date);
        Assert.Null(shared.ExpiresAt);
    }

    [Fact]
    public async Task The_shared_view_reflects_entries_logged_after_the_link_was_made()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var link = await CreateShareLinkAsync(fixture, token, child.Id);
        var query = $"?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(13):yyyy-MM-dd}";

        Assert.Empty((await GetSharedAsync(fixture, link!.Token, query))!.Entries);

        await LogAsync(fixture, token, child.Id, Monday, FullNight());

        Assert.Single((await GetSharedAsync(fixture, link.Token, query))!.Entries);
    }

    [Fact]
    public async Task An_unknown_token_gets_not_found_and_a_bad_range_gets_bad_request()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var link = await CreateShareLinkAsync(fixture, token, child.Id);

        await GetSharedAsync(fixture, "not-a-real-token", expectedStatus: 404);
        await GetSharedAsync(fixture, link!.Token, $"?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(-1):yyyy-MM-dd}", expectedStatus: 400);
    }

    [Fact]
    [CoversEndpoint("ListSleepDiaryShareLinks")]
    [CoversEndpoint("RevokeSleepDiaryShareLink")]
    public async Task A_revoked_link_stops_working_and_leaves_the_list()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var first = await CreateShareLinkAsync(fixture, token, child.Id);
        var second = await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddDays(7));

        var listed = await ListShareLinksAsync(fixture, token, child.Id);
        Assert.Equal([second!.Id, first!.Id], listed!.Select(l => l.Id));

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
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        // The API refuses a past expiry, so the expired link is written straight to the store.
        var store = fixture.Host.Services.GetRequiredService<ISleepDiaryShareTokenEventStore>();
        var (plaintext, hash) = SleepDiaryShareSecret.Generate();
        var id = SleepDiaryShareTokenId.New();
        await store.CreateAsync(
            id,
            [new SleepDiaryShareTokenCreated(id, new UserId(child.Id), hash, new UserId(guardianId), DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(-1))],
            CancellationToken.None);

        await GetSharedAsync(fixture, plaintext, expectedStatus: 404);
        Assert.Empty((await ListShareLinksAsync(fixture, token, child.Id))!);
    }

    [Fact]
    public async Task A_past_or_too_distant_expiry_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");

        await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddMinutes(-1), expectedStatus: 400);
        await CreateShareLinkAsync(fixture, token, child.Id, DateTimeOffset.UtcNow.AddDays(CreateSleepDiaryShareLinkValidator.MaxExpiryDays + 1), expectedStatus: 400);
    }

    [Fact]
    public async Task The_child_and_an_unrelated_user_cannot_create_list_or_revoke_links()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var link = await CreateShareLinkAsync(fixture, token, child.Id);

        foreach (var other in new[] { childToken, strangerToken })
        {
            await CreateShareLinkAsync(fixture, other, child.Id, expectedStatus: 404);
            await ListShareLinksAsync(fixture, other, child.Id, expectedStatus: 404);
            await RevokeShareLinkAsync(fixture, other, child.Id, link!.Id, expectedStatus: 404);
        }

        await GetSharedAsync(fixture, link!.Token);
    }

    [Fact]
    public async Task A_link_cannot_be_revoked_through_another_child()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var alex = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var sam = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Sam");
        var link = await CreateShareLinkAsync(fixture, token, alex.Id);

        await RevokeShareLinkAsync(fixture, token, sam.Id, link!.Id, expectedStatus: 404);
        await RevokeShareLinkAsync(fixture, token, alex.Id, Guid.CreateVersion7(), expectedStatus: 404);

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
