using Alba;

using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.ReplacePrintTemplateGuardianColors;

[Collection(BuddyApiCollection.Name)]
public sealed class ReplacePrintTemplateGuardianColorsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ReplacePrintTemplateGuardianColors")]
    public async Task A_guardian_colors_themself_and_their_co_guardian()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, family.FirstToken);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.FirstToken}");
            _.Put.Json(new
            {
                Colors = new[]
                {
                    new { GuardianId = family.FirstId, Color = "#0ea5e9" },
                    new { GuardianId = family.SecondId, Color = "#f43f5e" },
                }
            }).ToUrl($"/print-templates/{template.Id}/colors");
            _.StatusCodeShouldBeOk();
        });

        var read = await PrintTemplateTestHelpers.GetAsync(fixture, family.FirstToken, template.Id);
        Assert.Equal(
            [new GuardianColorDto(family.FirstId, "#0ea5e9"), new GuardianColorDto(family.SecondId, "#f43f5e")],
            read.GuardianColors);
    }

    [Fact]
    public async Task A_guardian_who_shares_no_child_is_rejected()
    {
        var (_, _, strangerId) = await fixture.CreateAuthenticatedUserAsync();
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Colors = new[] { new { GuardianId = strangerId, Color = "#0ea5e9" } } }).ToUrl($"/print-templates/{template.Id}/colors");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task Two_colors_for_one_guardian_or_a_blank_color_are_rejected()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        foreach (var colors in new object[]
        {
            new[] { new { GuardianId = userId, Color = "#0ea5e9" }, new { GuardianId = userId, Color = "#f43f5e" } },
            new[] { new { GuardianId = userId, Color = "" } },
        })
        {
            await fixture.Host.Scenario(_ =>
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
                _.Put.Json(new { Colors = colors }).ToUrl($"/print-templates/{template.Id}/colors");
                _.StatusCodeShouldBe(400);
            });
        }
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken);
        var (_, strangerToken, strangerId) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Put.Json(new { Colors = new[] { new { GuardianId = strangerId, Color = "#0ea5e9" } } }).ToUrl($"/print-templates/{template.Id}/colors");
            _.StatusCodeShouldBe(404);
        });
    }
}
