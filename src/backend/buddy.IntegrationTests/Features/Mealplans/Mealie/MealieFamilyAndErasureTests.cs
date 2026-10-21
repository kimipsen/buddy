using Alba;

using buddy.Common.Erasure;
using buddy.Features.Mealplans;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// Who reaches a family's Mealie connection, and what erasing a guardian does to it --
// docs/backend/analysis/mealie-integration.md, Questions 2 and 7.
[Collection(BuddyApiCollection.Name)]
public sealed class MealieFamilyAndErasureTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task A_guardian_who_unlinks_the_child_holding_the_connection_no_longer_reaches_it()
    {
        // Unlike an AI key, which follows the guardian who added it: a Mealie token is a whole
        // Mealie user, so it stays with the family it was connected for.
        var family = await MealieTestHost.CreateFamilyAsync(fixture);
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, family.Token, "Ida");
        Assert.Equal(1, (await (family with { Child = sibling }).GetAsync("mealie")).GetProperty("kind").GetInt32());

        await RevokeLinkAsync(family.Token, family.ChildId);

        Assert.Equal(0, (await (family with { Child = sibling }).GetAsync("mealie")).GetProperty("kind").GetInt32());
    }

    [Fact]
    public async Task Erasing_the_guardian_who_connected_disconnects_the_family_and_masks_the_connection()
    {
        var co = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var host = await MealieTestHost.GetAsync(fixture);
        var server = FakeMealieClient.Instance.NewServer();
        var first = new MealieFamily(host, co.FirstToken, co.FirstId, co.Child, server);
        var second = first with { Token = co.SecondToken, GuardianId = co.SecondId };
        await first.ConnectAsync();
        Assert.Equal(1, (await second.GetAsync("mealie")).GetProperty("kind").GetInt32());

        await DeleteAccountAsync(co.FirstToken);

        Assert.Equal(0, (await second.GetAsync("mealie")).GetProperty("kind").GetInt32());
        var connected = Assert.Single((await ReadConnectionEventsAsync(co.Child.Id)).Select(e => e.Value).OfType<MealieConnected>());
        Assert.Equal(Erased.Text, connected.BaseUrl);
        Assert.Equal(Erased.Text, connected.MealieUserName);
        Assert.Equal("", connected.Token.CipherText);
        Assert.Equal("", connected.Token.Last4);
    }

    [Fact]
    public async Task Erasing_a_guardian_finds_a_connection_indexed_under_a_sibling_they_never_guarded()
    {
        // The co-guardian connects through a child of their own; the erased guardian reconnects the
        // same stream through the shared child. The stream's index names only the co-guardian's child.
        var co = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var host = await MealieTestHost.GetAsync(fixture);
        var server = FakeMealieClient.Instance.NewServer();
        server.ValidTokens.Add("first-token-5678");
        var ownChild = await GuardianTestHelpers.CreateChildAsync(fixture, co.SecondToken, "Ida");
        var second = new MealieFamily(host, co.SecondToken, co.SecondId, ownChild, server);
        var first = new MealieFamily(host, co.FirstToken, co.FirstId, co.Child, server);
        await second.ConnectAsync();
        await first.ConnectAsync(apiToken: "first-token-5678");

        await DeleteAccountAsync(co.FirstToken);

        // The erased guardian's connect is the current one, so the family is disconnected, and only
        // the erased guardian's connect is masked.
        Assert.Equal(0, (await second.GetAsync("mealie")).GetProperty("kind").GetInt32());
        var connects = (await ReadConnectionEventsAsync(ownChild.Id)).Select(e => e.Value).OfType<MealieConnected>().ToList();
        Assert.Equal(2, connects.Count);
        Assert.Equal(server.BaseUrl, connects[0].BaseUrl);
        Assert.Equal(Erased.Text, connects[1].BaseUrl);
    }

    private async Task<IReadOnlyCollection<MealieConnectionEvent>> ReadConnectionEventsAsync(Guid childId)
    {
        var connections = fixture.Host.Services.GetRequiredService<IMealieConnectionEventStore>();
        var id = await connections.FindIdForChildAsync(new UserId(childId), CancellationToken.None);
        Assert.NotNull(id);

        return await connections.ReadAsync(id, CancellationToken.None);
    }

    private Task<IScenarioResult> RevokeLinkAsync(string token, Guid childId) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/users/me/children/{childId}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

    private Task<IScenarioResult> DeleteAccountAsync(string token) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(204);
        });
}
