using Alba;

using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.ReplacePrintTemplateBabysitterColors;

[Collection(BuddyApiCollection.Name)]
public sealed class ReplacePrintTemplateBabysitterColorsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ReplacePrintTemplateBabysitterColors")]
    public async Task A_guardian_colors_their_own_and_their_co_guardians_babysitters()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var mine = await BabysitterTestHelpers.AddAsync(fixture, family.FirstToken, "Mette");
        var theirs = await BabysitterTestHelpers.AddAsync(fixture, family.SecondToken, "Jonas");
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, family.FirstToken);

        var response = await Replace(family.FirstToken, template.Id,
        [
            new { GuardianId = family.FirstId, BabysitterId = mine.Id, Color = " #a855f7 " },
            new { GuardianId = family.SecondId, BabysitterId = theirs.Id, Color = "#f59e0b" },
        ]);

        BabysitterColorDto[] expected =
        [
            new(family.FirstId, mine.Id, "#a855f7"),
            new(family.SecondId, theirs.Id, "#f59e0b"),
        ];
        Assert.Equal(expected, response.ReadAsJson<PrintTemplateDto>().BabysitterColors);

        var read = await PrintTemplateTestHelpers.GetAsync(fixture, family.FirstToken, template.Id);
        Assert.Equal(expected, read.BabysitterColors);
        Assert.Empty(read.GuardianColors);
    }

    [Fact]
    public async Task A_babysitter_archived_after_being_colored_does_not_block_later_saves()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var mette = await BabysitterTestHelpers.AddAsync(fixture, token, "Mette");
        var jonas = await BabysitterTestHelpers.AddAsync(fixture, token, "Jonas");
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await Replace(token, template.Id, [new { GuardianId = userId, BabysitterId = mette.Id, Color = "#a855f7" }]);
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, mette.Id);

        await Replace(token, template.Id,
        [
            new { GuardianId = userId, BabysitterId = mette.Id, Color = "#a855f7" },
            new { GuardianId = userId, BabysitterId = jonas.Id, Color = "#f59e0b" },
        ]);
    }

    [Fact]
    public async Task An_archived_or_unknown_babysitter_is_rejected()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var archived = await BabysitterTestHelpers.AddAsync(fixture, token, "Mette");
        await BabysitterTestHelpers.ArchiveAsync(fixture, token, archived.Id);
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        foreach (var babysitterId in new[] { archived.Id, Guid.CreateVersion7() })
        {
            await Replace(token, template.Id, [new { GuardianId = userId, BabysitterId = babysitterId, Color = "#a855f7" }], expectedStatus: 400);
        }
    }

    [Fact]
    public async Task A_babysitter_of_a_guardian_who_shares_no_child_is_rejected()
    {
        var (_, strangerToken, strangerId) = await fixture.CreateAuthenticatedUserAsync();
        var strangers = await BabysitterTestHelpers.AddAsync(fixture, strangerToken, "Mette");
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await Replace(token, template.Id, [new { GuardianId = strangerId, BabysitterId = strangers.Id, Color = "#a855f7" }], expectedStatus: 400);
    }

    [Fact]
    public async Task Two_colors_for_one_babysitter_or_a_blank_color_are_rejected()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var mette = await BabysitterTestHelpers.AddAsync(fixture, token, "Mette");
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await Replace(token, template.Id,
        [
            new { GuardianId = userId, BabysitterId = mette.Id, Color = "#a855f7" },
            new { GuardianId = userId, BabysitterId = mette.Id, Color = "#f59e0b" },
        ], expectedStatus: 400);
        await Replace(token, template.Id, [new { GuardianId = userId, BabysitterId = mette.Id, Color = "  " }], expectedStatus: 400);
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken);
        var (_, strangerToken, strangerId) = await fixture.CreateAuthenticatedUserAsync();
        var mette = await BabysitterTestHelpers.AddAsync(fixture, strangerToken, "Mette");

        await Replace(strangerToken, template.Id, [new { GuardianId = strangerId, BabysitterId = mette.Id, Color = "#a855f7" }], expectedStatus: 404);
        await Replace(strangerToken, Guid.CreateVersion7(), [], expectedStatus: 404);
    }

    private async Task<IScenarioResult> Replace(string token, Guid templateId, object[] colors, int expectedStatus = 200) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Colors = colors }).ToUrl($"/print-templates/{templateId}/babysitter-colors");
            _.StatusCodeShouldBe(expectedStatus);
        });
}
