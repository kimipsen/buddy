using System.Text.Json;

using Alba;

using buddy.Common.FeatureFlags;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Features.SleepDiaries;
using buddy.IntegrationTests.Features.TaskLibrary;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Common.FeatureFlags;

// docs/backend/analysis/feature-flags.md. The data is written through the shared host (every feature
// on), then the same requests go to hosts with features off. Each request is checked for 200 on the
// shared host first, so the 404 on the other host can only come from the route not being mapped,
// never from a handler's NotFound.
[Collection(BuddyApiCollection.Name)]
public sealed class FeatureFlagsTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private IAlbaHost? _allOff;
    private IAlbaHost? _mealplanExtrasOff;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_allOff is not null)
        {
            await _allOff.DisposeAsync();
        }

        if (_mealplanExtrasOff is not null)
        {
            await _mealplanExtrasOff.DisposeAsync();
        }
    }

    // xunit creates the class per test, so each host starts lazily, only for the tests that use it.
    // Mealplans off, its sub-flags left at their default (on): they follow the parent.
    private async Task<IAlbaHost> AllOffAsync() =>
        _allOff ??= await fixture.CreateHostAsync(new Dictionary<string, string?>
        {
            ["Features:Mealplans"] = "false",
            ["Features:Medicines"] = "false",
            ["Features:SleepDiary"] = "false",
            ["Features:Pickups"] = "false",
            ["Features:Babysitters"] = "false",
            ["Features:WorkLocations"] = "false",
            ["Features:Printing"] = "false",
            ["Features:Progress"] = "false",
            ["Features:TaskLibrary"] = "false",
            ["Features:Help"] = "false"
        });

    private async Task<IAlbaHost> MealplanExtrasOffAsync() =>
        _mealplanExtrasOff ??= await fixture.CreateHostAsync(new Dictionary<string, string?>
        {
            ["Features:MealplanAiAssistant"] = "false",
            ["Features:MealplanImport"] = "false"
        });

    [Fact]
    [CoversEndpoint("GetFeatures")]
    public async Task An_anonymous_caller_sees_every_feature_on_by_default()
    {
        var flags = await GetFeaturesAsync(fixture.Host);

        Assert.Equal(new InstallationFeatures(true, true, true, true, true, true, true, true, true, true, true, true), flags);
    }

    [Fact]
    public async Task The_flags_endpoint_reports_disabled_features_and_sub_flags_follow_their_parent()
    {
        var flags = await GetFeaturesAsync(await AllOffAsync());

        Assert.Equal(new InstallationFeatures(false, false, false, false, false, false, false, false, false, false, false, false), flags);
        Assert.Equal(
            new InstallationFeatures(true, false, false, true, true, true, true, true, true, true, true, true),
            await GetFeaturesAsync(await MealplanExtrasOffAsync()));
    }

    [Fact]
    public async Task The_flags_are_serialized_with_camel_case_names()
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/features");
            _.StatusCodeShouldBe(200);
        });

        using var json = JsonDocument.Parse(await response.ReadAsTextAsync());
        Assert.True(json.RootElement.GetProperty("mealplanAiAssistant").GetBoolean());
        Assert.True(json.RootElement.GetProperty("sleepDiary").GetBoolean());
    }

    [Fact]
    public async Task Every_disabled_feature_answers_404_while_core_features_keep_working()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        string[] disabled =
        [
            $"/medicines/children/{child.Id}/schedules",
            $"/mealplans/children/{child.Id}/meals",
            $"/mealplans/children/{child.Id}/ai/providers",
            $"/mealplans/children/{child.Id}/imports",
            $"/sleep-diary/children/{child.Id}/share-links",
            $"/pickups/children/{child.Id}/schedule?from=2026-03-02&to=2026-03-08",
            "/babysitters/me",
            $"/work-locations/guardians/{guardianId}",
            "/print-templates/",
            $"/progress/children/{child.Id}",
            $"/task-templates/children/{child.Id}"
        ];

        foreach (var url in disabled)
        {
            await GetAsync(fixture.Host, url, token, 200);
            await GetAsync(await AllOffAsync(), url, token, 404);
        }

        await GetAsync(await AllOffAsync(), "/calendars", token, 200);
        await GetAsync(await AllOffAsync(), "/users/me", token, 200);
    }

    [Fact]
    public async Task The_anonymous_routes_of_disabled_features_answer_404()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var feed = await MealplanTestHelpers.CreateIcalTokenAsync(fixture, token, child.Id);
        var shareLink = await SleepDiaryTestHelpers.CreateShareLinkAsync(fixture, token, child.Id);

        string[] anonymous = [feed.SubscriptionPath, $"/sleep-diary/shared/{shareLink!.Token}"];

        foreach (var url in anonymous)
        {
            await GetAsync(fixture.Host, url, token: null, 200);
            await GetAsync(await AllOffAsync(), url, token: null, 404);
        }
    }

    [Fact]
    public async Task Scheduling_from_the_task_library_is_unmapped_when_the_task_library_is_off()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, "Routines");
        var template = await TaskLibraryTestHelpers.CreateTaskTemplateAsync(fixture, token, child.Id);
        await TaskLibraryTestHelpers.AddSubtaskAsync(fixture, token, template!.Id);

        await (await AllOffAsync()).Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new
            {
                TaskTemplateId = template!.Id,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
                StartTime = new TimeOnly(7, 0),
                Title = "Morning routine",
                Icon = "task",
                Color = "#ff0000"
            }).ToUrl($"/calendars/{calendarId}/items/from-template");
            // 405 rather than 404: routing matches the path against DELETE /items/{itemId:guid} and
            // rejects the method before it checks the guid constraint. Either way, nothing runs.
            _.StatusCodeShouldBe(405);
        });
    }

    [Fact]
    public async Task Turning_off_the_ai_assistant_and_import_leaves_the_rest_of_the_meal_plan()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);

        await GetAsync(await MealplanExtrasOffAsync(), $"/mealplans/children/{child.Id}/meals", token, 200);
        await GetAsync(await MealplanExtrasOffAsync(), $"/mealplans/children/{child.Id}/ai/providers", token, 404);
        await GetAsync(await MealplanExtrasOffAsync(), $"/mealplans/children/{child.Id}/imports", token, 404);
    }

    [Fact]
    public async Task A_disabled_features_data_is_still_in_the_personal_data_export()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token);
        var medicine = $"Medizor{Guid.CreateVersion7():N}";
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id, new CreateMedicineScheduleOptions(Name: medicine));

        var response = await (await AllOffAsync()).Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/export");
            _.StatusCodeShouldBe(200);
        });

        Assert.Contains(medicine, await response.ReadAsTextAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_misspelt_flag_fails_the_host_at_startup()
    {
        var startup = fixture.CreateHostAsync(new Dictionary<string, string?> { ["Features:Medecines"] = "false" });

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => startup);
        Assert.Contains("Medecines", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_flag_that_is_not_a_boolean_fails_the_host_at_startup()
    {
        var startup = fixture.CreateHostAsync(new Dictionary<string, string?> { ["Features:Medicines"] = "no" });

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => startup);
        Assert.Contains("Features", exception.ToString(), StringComparison.Ordinal);
    }

    private static async Task<InstallationFeatures> GetFeaturesAsync(IAlbaHost host)
    {
        var response = await host.Scenario(_ =>
        {
            _.Get.Url("/features");
            _.StatusCodeShouldBe(200);
        });

        return response.ReadAsJson<InstallationFeatures>();
    }

    private static Task<IScenarioResult> GetAsync(IAlbaHost host, string url, string? token, int expectedStatus) =>
        host.Scenario(_ =>
        {
            if (token is not null)
            {
                _.WithRequestHeader("Authorization", $"Bearer {token}");
            }

            _.Get.Url(url);
            _.StatusCodeShouldBe(expectedStatus);
        });
}
