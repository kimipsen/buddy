using Alba;

using buddy.Features.Users;

using Microsoft.Extensions.DependencyInjection;

using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Import;

internal sealed record ImportPreviewLineDto(int LineNumber, DateOnly Date, MealSlot Slot, string RawText, ImportLineKind Kind, string MealName, string Notes, string Key, bool Occupied);

internal sealed record ImportPreviewGroupDto(
    string Key, string Name, ImportLineKind Kind, int Count, DateOnly FirstDate, DateOnly LastDate,
    ImportGroupAction DefaultAction, Guid? MatchedMealId, string MatchedMealName, Guid? SuggestedMealId, string SuggestedGroupKey, string SuggestedName);

internal sealed record ImportWarningDto(int LineNumber, string Code, string Message);

internal sealed record ImportPreviewDto(string Format, List<ImportPreviewLineDto> Lines, List<ImportPreviewGroupDto> Groups, List<ImportWarningDto> Warnings, int EmptyDays);

internal sealed record SkippedImportEntryDto(DateOnly Date, MealSlot Slot, string Reason);

internal sealed record ImportResultDto(Guid? ImportId, int Imported, int CreatedMeals, int ArchivedMeals, List<SkippedImportEntryDto> Skipped);

internal sealed record ImportSummaryDto(Guid ImportId, string Format, DateOnly From, DateOnly To, int EntryCount, int CreatedMealCount, Guid ImportedBy, DateTimeOffset ImportedAt, bool Reverted);

// Covers docs/backend/analysis/mealplan-import.md end to end through the API.
[Collection(BuddyApiCollection.Name)]
public sealed class MealPlanImportTests(BuddyApiFixture fixture)
{
    // ISO week 3 of 2024 starts Monday 15 January; the note's Sunday is the 14th.
    private const string Note = """
        Madplan 2024
        U3
        Sø: Lasagne
        Ma: Hotdogs 🌭
        Ti: hotdogs
        On: Rester
        To: Pizza + GS
        """;

    private static readonly DateOnly Sunday = new(2024, 1, 14);

    [Fact]
    [CoversEndpoint("PreviewMealPlanImport")]
    [CoversEndpoint("CommitMealPlanImport")]
    [CoversEndpoint("ListMealPlanImports")]
    [CoversEndpoint("RevertMealPlanImport")]
    public async Task A_guardian_can_preview_commit_list_and_revert_an_import()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var lasagne = await MealplanTestHelpers.CreateMealAsync(fixture, token, child.Id, new CreateMealOptions(Name: "Lasagne"));
        Assert.NotNull(lasagne);
        var basePath = $"/mealplans/children/{child.Id}";

        // Thursday is already planned by hand: the import must leave it alone.
        await AssignAsync(token, basePath, Sunday.AddDays(4), lasagne.Id);

        var preview = await PreviewAsync(token, basePath, Note);

        Assert.Equal("weekly-note", preview.Format);
        var lasagneGroup = Assert.Single(preview.Groups, g => g.Key == "lasagne");
        Assert.Equal((ImportGroupAction.Existing, lasagne.Id), (lasagneGroup.DefaultAction, lasagneGroup.MatchedMealId));
        Assert.Equal((ImportGroupAction.New, 2), Assert.Single(preview.Groups, g => g.Key == "hotdogs") is var hotdogs ? (hotdogs.DefaultAction, hotdogs.Count) : default);
        Assert.Equal(ImportGroupAction.Skip, Assert.Single(preview.Groups, g => g.Key == "rester").DefaultAction);
        Assert.True(Assert.Single(preview.Lines, l => l.Key == "pizza").Occupied);
        Assert.Single(await ListPlanAsync(token, basePath));

        // ArchiveSingleUse off: Tacos is used once, and is assigned by hand below (an archived meal
        // can't be). Old_meals_used_once_are_archived... covers the archiving.
        var result = await CommitAsync(token, basePath, new
        {
            Format = "weekly-note",
            ArchiveSingleUse = false,
            Entries = new object[]
            {
                new { Date = Sunday, Slot = MealSlot.Dinner, MealId = lasagne.Id },
                new { Date = Sunday.AddDays(1), Slot = MealSlot.Dinner, NewMealName = "Hotdogs" },
                new { Date = Sunday.AddDays(2), Slot = MealSlot.Dinner, NewMealName = "hotdogs" },
                new { Date = Sunday.AddDays(4), Slot = MealSlot.Dinner, NewMealName = "Pizza", Notes = "+ GS" },
                new { Date = Sunday.AddDays(5), Slot = MealSlot.Dinner, NewMealName = "Tacos" },
            },
        });

        Assert.NotNull(result.ImportId);
        Assert.Equal((4, 2, 0), (result.Imported, result.CreatedMeals, result.ArchivedMeals));
        Assert.Equal((Sunday.AddDays(4), "occupied"), Assert.Single(result.Skipped) is var skipped ? (skipped.Date, skipped.Reason) : default);

        var plan = await ListPlanAsync(token, basePath);
        Assert.Equal(5, plan.Count);
        Assert.Equal("Hotdogs", Assert.Single(plan, e => e.Date == Sunday.AddDays(1)).MealName);
        Assert.Equal("Lasagne", Assert.Single(plan, e => e.Date == Sunday.AddDays(4)).MealName);

        var imports = await ListImportsAsync(token, basePath);
        var summary = Assert.Single(imports);
        Assert.Equal((result.ImportId.Value, 4, 2, Sunday, Sunday.AddDays(5), false), (summary.ImportId, summary.EntryCount, summary.CreatedMealCount, summary.From, summary.To, summary.Reverted));

        // A guardian changes one imported day by hand, and plans the imported Tacos on another day;
        // the revert must keep both, and Tacos (still in use) must stay in the library.
        await AssignAsync(token, basePath, Sunday.AddDays(1), lasagne.Id);
        var tacosId = Assert.Single(plan, e => e.Date == Sunday.AddDays(5)).MealId;
        await AssignAsync(token, basePath, Sunday.AddDays(6), tacosId);

        await RevertAsync(token, basePath, result.ImportId.Value, 204);

        var afterRevert = await ListPlanAsync(token, basePath);
        Assert.Equal([Sunday.AddDays(1), Sunday.AddDays(4), Sunday.AddDays(6)], afterRevert.Select(e => e.Date).Order());

        // Hotdogs, created by the import and no longer used anywhere, is archived.
        var meals = await ListMealsAsync(token, basePath);
        Assert.True(Assert.Single(meals, m => m.Name == "Hotdogs").IsArchived);
        Assert.False(Assert.Single(meals, m => m.Name == "Lasagne").IsArchived);
        Assert.False(Assert.Single(meals, m => m.Name == "Tacos").IsArchived);

        var mealPlans = fixture.Host.Services.GetRequiredService<IMealPlanEventStore>();
        var planId = await mealPlans.FindIdForChildAsync(new UserId(child.Id), CancellationToken.None);
        Assert.NotNull(planId);
        var eventCount = (await mealPlans.ReadAsync(planId, CancellationToken.None)).Count;

        await RevertAsync(token, basePath, result.ImportId.Value, 204);
        Assert.True(Assert.Single(await ListImportsAsync(token, basePath)).Reverted);
        Assert.Equal(eventCount, (await mealPlans.ReadAsync(planId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task Committing_the_same_import_twice_writes_nothing_the_second_time()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var basePath = $"/mealplans/children/{child.Id}";
        var body = new
        {
            Format = "csv",
            ArchiveSingleUse = false,
            Entries = new[] { new { Date = Sunday, Slot = MealSlot.Dinner, NewMealName = "Tacos" } },
        };

        var first = await CommitAsync(token, basePath, body);
        var second = await CommitAsync(token, basePath, body);

        Assert.Equal((1, 1), (first.Imported, first.CreatedMeals));
        Assert.Equal((null, 0, 0), (second.ImportId, second.Imported, second.CreatedMeals));
        Assert.Single(second.Skipped);
        Assert.Single(await ListMealsAsync(token, basePath));
        Assert.Single(await ListImportsAsync(token, basePath));
    }

    [Fact]
    public async Task Reverting_an_import_leaves_the_days_a_later_import_wrote_with_the_same_meal()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var soup = await MealplanTestHelpers.CreateMealAsync(fixture, token, child.Id, new CreateMealOptions(Name: "Soup"));
        Assert.NotNull(soup);
        var basePath = $"/mealplans/children/{child.Id}";
        var body = new { Format = "csv", Entries = new[] { new { Date = Sunday, Slot = MealSlot.Dinner, MealId = soup.Id } } };

        var first = await CommitAsync(token, basePath, body);
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"{basePath}/plan").QueryString("date", $"{Sunday:yyyy-MM-dd}").QueryString("slot", "Dinner");
            _.StatusCodeShouldBe(204);
        });
        var second = await CommitAsync(token, basePath, body);
        Assert.Equal(1, second.Imported);

        await RevertAsync(token, basePath, first.ImportId!.Value, 204);

        Assert.Equal(soup.Id, Assert.Single(await ListPlanAsync(token, basePath)).MealId);
    }

    [Fact]
    public async Task Old_meals_used_once_are_archived_when_asked_and_stay_readable_in_the_plan()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var basePath = $"/mealplans/children/{child.Id}";

        var result = await CommitAsync(token, basePath, new
        {
            Format = "weekly-note",
            ArchiveSingleUse = true,
            Entries = new[]
            {
                new { Date = Sunday, Slot = MealSlot.Dinner, NewMealName = "Ferskensuppe" },
                new { Date = Sunday.AddDays(1), Slot = MealSlot.Dinner, NewMealName = "Nachos" },
                new { Date = Sunday.AddDays(2), Slot = MealSlot.Dinner, NewMealName = "Nachos" },
            },
        });

        Assert.Equal((2, 1), (result.CreatedMeals, result.ArchivedMeals));
        var meals = await ListMealsAsync(token, basePath);
        Assert.True(Assert.Single(meals, m => m.Name == "Ferskensuppe").IsArchived);
        Assert.False(Assert.Single(meals, m => m.Name == "Nachos").IsArchived);
        Assert.Equal("Ferskensuppe", Assert.Single(await ListPlanAsync(token, basePath), e => e.Date == Sunday).MealName);
    }

    [Fact]
    public async Task Invalid_imports_are_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var basePath = $"/mealplans/children/{child.Id}";
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var otherChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherToken, "Sam");
        var foreignMeal = await MealplanTestHelpers.CreateMealAsync(fixture, otherToken, otherChild.Id);
        Assert.NotNull(foreignMeal);

        await PreviewAsync(token, basePath, Note, format: "spreadsheet", expectedStatus: 400);
        await PreviewAsync(token, basePath, "Just some text\nthat is not a plan", expectedStatus: 400);
        await PreviewAsync(token, basePath, "U2\nMa: Burger", format: "weekly-note", expectedStatus: 400);

        var tooMany = Enumerable.Range(0, CommitMealPlanImportRules.MaxEntries + 1)
            .Select(i => new { Date = Sunday.AddDays(i), Slot = MealSlot.Dinner, NewMealName = "Soup" })
            .ToArray();
        await CommitAsync(token, basePath, new { Format = "csv", Entries = tooMany }, expectedStatus: 400);

        await CommitAsync(token, basePath, new
        {
            Format = "csv",
            Entries = new[] { new { Date = Sunday, Slot = MealSlot.Dinner, MealId = foreignMeal.Id, NewMealName = "Soup" } },
        }, expectedStatus: 400);

        await CommitAsync(token, basePath, new
        {
            Format = "csv",
            Entries = new[] { new { Date = Sunday, Slot = MealSlot.Dinner, NewMealName = "Soup" }, new { Date = Sunday, Slot = MealSlot.Dinner, NewMealName = "Pie" } },
        }, expectedStatus: 400);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Format = "csv", Entries = new object?[] { null } }).ToUrl($"{basePath}/imports");
            _.StatusCodeShouldBe(400);
        });

        // A meal from another family's library.
        await CommitAsync(token, basePath, new
        {
            Format = "csv",
            Entries = new[] { new { Date = Sunday, Slot = MealSlot.Dinner, MealId = foreignMeal.Id } },
        }, expectedStatus: 400);

        Assert.Empty(await ListMealsAsync(token, basePath));
    }

    [Fact]
    public async Task Only_the_childs_guardians_can_import()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var basePath = $"/mealplans/children/{child.Id}";
        var body = new { Format = "csv", Entries = new[] { new { Date = Sunday, Slot = MealSlot.Dinner, NewMealName = "Soup" } } };

        await PreviewAsync(strangerToken, basePath, Note, expectedStatus: 404);
        await CommitAsync(strangerToken, basePath, body, expectedStatus: 404);
        await ListImportsAsync(strangerToken, basePath, expectedStatus: 404);
        await RevertAsync(strangerToken, basePath, Guid.CreateVersion7(), 404);

        await PreviewAsync(childToken, basePath, Note, expectedStatus: 403);
        await CommitAsync(childToken, basePath, body, expectedStatus: 403);
        await ListImportsAsync(childToken, basePath, expectedStatus: 403);
        await RevertAsync(childToken, basePath, Guid.CreateVersion7(), 403);

        // An unknown import id on a family that has a plan.
        await CommitAsync(token, basePath, body);
        await RevertAsync(token, basePath, Guid.CreateVersion7(), 404);
    }

    [Fact]
    [CoversEndpoint("PreviewMealPlanImportForGroup")]
    [CoversEndpoint("CommitMealPlanImportForGroup")]
    [CoversEndpoint("ListMealPlanImportsForGroup")]
    [CoversEndpoint("RevertMealPlanImportForGroup")]
    public async Task A_manage_tier_group_member_can_import_and_a_view_tier_member_cannot()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var groupId = await MealplanTestHelpers.ShareWithNewGroupAsync(fixture, token, child.Id);
        var groupPath = $"/mealplans/groups/{groupId}";

        var (_, viewerToken, viewerId) = await fixture.CreateAuthenticatedUserAsync();
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Role = GroupRole.Member }).ToUrl($"/groups/{groupId}/members/{viewerId}");
            _.StatusCodeShouldBe(204);
        });
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new
            {
                Policy = new Dictionary<GroupRole, MealplanAccessTier>
                {
                    [GroupRole.Owner] = MealplanAccessTier.Manage,
                    [GroupRole.Admin] = MealplanAccessTier.Manage,
                    [GroupRole.Member] = MealplanAccessTier.View,
                },
            }).ToUrl($"/groups/{groupId}/mealplan-permission-policy");
            _.StatusCodeShouldBe(204);
        });

        var preview = await PreviewAsync(token, groupPath, Note);
        Assert.Equal(4, preview.Lines.Count(l => l.Kind != ImportLineKind.Leftovers));

        var body = new { Format = "weekly-note", Entries = new[] { new { Date = Sunday, Slot = MealSlot.Dinner, NewMealName = "Lasagne" } } };
        var result = await CommitAsync(token, groupPath, body);
        Assert.NotNull(result.ImportId);
        Assert.Single(await ListImportsAsync(token, groupPath));

        // The import landed in the family's own plan.
        Assert.Equal("Lasagne", Assert.Single(await ListPlanAsync(token, $"/mealplans/children/{child.Id}")).MealName);

        await PreviewAsync(viewerToken, groupPath, Note, expectedStatus: 403);
        await CommitAsync(viewerToken, groupPath, body, expectedStatus: 403);
        await ListImportsAsync(viewerToken, groupPath, expectedStatus: 403);
        await RevertAsync(viewerToken, groupPath, result.ImportId.Value, 403);

        await RevertAsync(token, groupPath, result.ImportId.Value, 204);
        Assert.Empty(await ListPlanAsync(token, $"/mealplans/children/{child.Id}"));

        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await PreviewAsync(strangerToken, groupPath, Note, expectedStatus: 404);
    }

    private async Task<ImportPreviewDto> PreviewAsync(string token, string basePath, string text, string? format = null, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Text = text, Format = format }).ToUrl($"{basePath}/imports/preview");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<ImportPreviewDto>() : null!;
    }

    private async Task<ImportResultDto> CommitAsync(string token, string basePath, object body, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(body).ToUrl($"{basePath}/imports");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<ImportResultDto>() : null!;
    }

    private async Task<List<ImportSummaryDto>> ListImportsAsync(string token, string basePath, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"{basePath}/imports");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<List<ImportSummaryDto>>() : [];
    }

    private async Task RevertAsync(string token, string basePath, Guid importId, int expectedStatus) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"{basePath}/imports/{importId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private async Task AssignAsync(string token, string basePath, DateOnly date, Guid mealId) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { MealId = mealId }).ToUrl($"{basePath}/plan").QueryString("date", $"{date:yyyy-MM-dd}").QueryString("slot", "Dinner");
            _.StatusCodeShouldBeOk();
        });

    private async Task<List<MealPlanEntryDto>> ListPlanAsync(string token, string basePath)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"{basePath}/plan?from={Sunday:yyyy-MM-dd}&to={Sunday.AddDays(6):yyyy-MM-dd}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<MealPlanEntryDto>>();
    }

    private async Task<List<MealDto>> ListMealsAsync(string token, string basePath)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"{basePath}/meals");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<List<MealDto>>();
    }
}
