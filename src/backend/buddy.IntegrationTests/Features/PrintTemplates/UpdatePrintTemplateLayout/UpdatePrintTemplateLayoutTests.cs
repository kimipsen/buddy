using Alba;

using buddy.Features.PrintTemplates;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.PrintTemplates.UpdatePrintTemplateLayout;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdatePrintTemplateLayoutTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdatePrintTemplateLayout")]
    public async Task A_guardian_switches_to_A3_with_a_sunday_start_and_no_week_number()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { PaperSize = 1, DefaultStartWeekday = 0, ShowWeekNumber = false }).ToUrl($"/print-templates/{created.Id}/layout");
            _.StatusCodeShouldBeOk();
        });

        var read = await PrintTemplateTestHelpers.GetAsync(fixture, token, created.Id);
        Assert.Equal(PaperSize.A3, read.PaperSize);
        Assert.Equal(DayOfWeek.Sunday, read.DefaultStartWeekday);
        Assert.False(read.ShowWeekNumber);
    }

    [Fact]
    public async Task Saving_the_current_layout_appends_no_event()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, token);
        var store = fixture.Host.Services.GetRequiredService<IPrintTemplateEventStore>();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { PaperSize = 0, DefaultStartWeekday = 1, ShowWeekNumber = true }).ToUrl($"/print-templates/{created.Id}/layout");
            _.StatusCodeShouldBeOk();
        });

        Assert.Single(await store.ReadAsync(new PrintTemplateId(created.Id), CancellationToken.None));
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(0, 7)]
    public async Task An_unknown_paper_size_or_weekday_is_rejected(int paperSize, int weekday)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, token);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { PaperSize = paperSize, DefaultStartWeekday = weekday, ShowWeekNumber = true }).ToUrl($"/print-templates/{created.Id}/layout");
            _.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var created = await PrintTemplateTestHelpers.CreateAsync(fixture, ownerToken);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {strangerToken}");
            _.Patch.Json(new { PaperSize = 1, DefaultStartWeekday = 1, ShowWeekNumber = true }).ToUrl($"/print-templates/{created.Id}/layout");
            _.StatusCodeShouldBe(404);
        });
    }
}
