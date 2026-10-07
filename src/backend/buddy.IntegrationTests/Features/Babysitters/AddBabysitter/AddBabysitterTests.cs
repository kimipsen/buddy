using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Babysitters.AddBabysitter;

[Collection(BuddyApiCollection.Name)]
public sealed class AddBabysitterTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("AddBabysitter")]
    public async Task A_guardian_can_add_a_babysitter_and_read_it_back()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var added = await BabysitterTestHelpers.AddAsync(fixture, token, "  Anna  ", " +45 12 34 56 78 ");

        Assert.Equal("Anna", added.Name);
        Assert.Equal("+45 12 34 56 78", added.ContactInfo);
        Assert.False(added.IsArchived);
        Assert.Equal(added, Assert.Single(await BabysitterTestHelpers.ListMineAsync(fixture, token)));
    }

    [Fact]
    public async Task Contact_info_is_optional()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var added = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        Assert.Equal("", added.ContactInfo);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("A name that is far, far too long to be the name of any real babysitter, nanny or au pair", "")]
    public async Task Missing_or_oversized_details_are_rejected(string name, string contactInfo)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = name, ContactInfo = contactInfo }).ToUrl("/babysitters/me");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Oversized_contact_info_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Anna", ContactInfo = new string('x', 201) }).ToUrl("/babysitters/me");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_name_already_used_by_an_active_babysitter_is_rejected_case_insensitively()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "ANNA" }).ToUrl("/babysitters/me");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task An_archived_babysitters_name_can_be_reused()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var first = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, first.Id);

        var second = await BabysitterTestHelpers.AddAsync(fixture, token, "Anna");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task A_guardian_can_have_at_most_twenty_active_babysitters()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        for (var i = 1; i <= 20; i++)
        {
            await BabysitterTestHelpers.AddAsync(fixture, token, $"Babysitter {i}");
        }

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "One too many" }).ToUrl("/babysitters/me");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_child_account_is_forbidden()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {childToken}");
            _.Post.Json(new { Name = "Anna" }).ToUrl("/babysitters/me");
            _.StatusCodeShouldBe(403);
        });
    }
}
