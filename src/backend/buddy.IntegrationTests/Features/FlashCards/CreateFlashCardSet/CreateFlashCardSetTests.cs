using buddy.Features.FlashCards;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.FlashCards.FlashCardTestHelpers;

namespace buddy.IntegrationTests.Features.FlashCards.CreateFlashCardSet;

[Collection(BuddyApiCollection.Name)]
public sealed class CreateFlashCardSetTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("CreateFlashCardSet")]
    public async Task A_guardian_can_create_a_text_set()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();

        var set = (await PostAsync(fixture, token, "/flash-cards/sets", new { Name = "  Capitals  ", Kind = "Text" })).ReadAsJson<FlashCardSetResponse>();

        Assert.Equal("Capitals", set.Name);
        Assert.Equal(FlashCardSetKind.Text, set.Kind);
        Assert.Equal(guardianId, set.OwnerId);
        Assert.Null(set.Math);
        Assert.Empty(set.Cards);
        Assert.Equal(0, set.UniqueCardCount);
    }

    [Fact]
    public async Task A_guardian_can_create_a_math_set_whose_cards_are_generated()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var set = await CreateMathSetAsync(fixture, token);

        Assert.Equal(FlashCardSetKind.Math, set.Kind);
        Assert.Equal(new MathRule(MathOperation.Addition, 10), set.Math);
        Assert.Empty(set.Cards);
        Assert.Equal(MathDeck.UniqueCount(new MathRule(MathOperation.Addition, 10)), set.UniqueCardCount);
    }

    [Theory]
    [InlineData("", "Text", null, null, "name")]
    [InlineData("Capitals", "Text", "Addition", 10, "math")]
    [InlineData("Add", "Math", null, null, "math")]
    [InlineData("Add", "Math", "Addition", 1, "math")]
    [InlineData("Add", "Math", "Addition", 101, "math")]
    [InlineData("Add", "Math", "Multiplication", 13, "math")]
    [InlineData("Add", "Math", "Division", 0, "math")]
    [InlineData("Add", "Math", "Power", 3, null)]
    public async Task An_invalid_set_is_rejected(string name, string kind, string? operation, int? limit, string? field)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        object body = operation is null
            ? new { Name = name, Kind = kind }
            : new { Name = name, Kind = kind, Math = new { Operation = operation, Limit = limit } };

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(body).ToUrl("/flash-cards/sets");
            _.StatusCodeShouldBe(400);
        });

        if (field is not null)
        {
            Assert.Contains(field, await response.ReadAsTextAsync(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_name_over_the_limit_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await PostAsync(fixture, token, "/flash-cards/sets", new { Name = new string('x', FlashCardLimits.NameMaxLength + 1), Kind = "Text" }, 400);
        await PostAsync(fixture, token, "/flash-cards/sets", new { Name = new string('x', FlashCardLimits.NameMaxLength), Kind = "Text" });
    }
}
