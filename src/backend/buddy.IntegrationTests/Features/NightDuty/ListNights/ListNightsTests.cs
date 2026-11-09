using Alba;

using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

using static buddy.IntegrationTests.Features.NightDuty.NightDutyTestHelpers;

namespace buddy.IntegrationTests.Features.NightDuty.ListNights;

[Collection(BuddyApiCollection.Name)]
public sealed class ListNightsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ListNights")]
    public async Task The_child_sees_their_own_nights()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var monday = FutureMonday();
        await SetOverridesAsync(fixture, token, child.Id, Override(monday, Guardian(guardianId, "Screen night")));

        var night = Assert.Single(await ListNightsAsync(fixture, childToken, child.Id, monday, monday));

        Assert.Equal(guardianId, night.Status.Entry!.Responsible.GuardianId);
        Assert.Equal("Screen night", night.Status.Entry.Note);
    }

    [Fact]
    public async Task A_child_with_no_schedule_has_only_unplanned_nights()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var monday = FutureMonday();

        var nights = await ListNightsAsync(fixture, token, child.Id, monday, monday.AddDays(6));

        Assert.Equal(7, nights.Count);
        Assert.All(nights, n => Assert.Equal(KindUnplanned, n.Status.Kind));
    }

    [Fact]
    public async Task A_babysitter_rename_reaches_every_night()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var maja = await BabysitterTestHelpers.AddAsync(fixture, token, "Maja");
        var monday = FutureMonday();
        await SetOverridesAsync(fixture, token, child.Id, Override(monday, Babysitter(guardianId, maja.Id)));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Name = "Maja H.", ContactInfo = "" }).ToUrl($"/babysitters/me/{maja.Id}");
            _.StatusCodeShouldBeOk();
        });

        var night = Assert.Single(await ListNightsAsync(fixture, token, child.Id, monday, monday));
        Assert.Equal("Maja H.", night.Status.Entry!.Responsible.Name);
    }

    [Fact]
    public async Task A_revoked_guardian_and_a_stranger_get_not_found_and_a_too_long_range_is_rejected()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var monday = FutureMonday();

        await ListNightsExpectingAsync(fixture, family.SecondToken, family.Child.Id, monday, monday, 200);
        await ListNightsExpectingAsync(fixture, family.FirstToken, family.Child.Id, monday, monday.AddDays(32), 400);
        await ListNightsExpectingAsync(fixture, strangerToken, family.Child.Id, monday, monday, 404);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/users/me/children/{family.Child.Id}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

        await ListNightsExpectingAsync(fixture, family.SecondToken, family.Child.Id, monday, monday, 404);
    }
}
