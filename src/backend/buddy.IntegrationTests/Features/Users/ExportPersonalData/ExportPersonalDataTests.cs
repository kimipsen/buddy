using System.Text.Json;

using Alba;

using buddy.Common.Erasure;
using buddy.Features.Guardians;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Features.PrintTemplates;
using buddy.IntegrationTests.Features.SleepDiaries;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.ExportPersonalData;

// GET /users/me/export -- docs/backend/analysis/gdpr-data-protection.md, Question 5. Every personal
// value is unique to its test, so finding it in the download proves which section carried it.
[Collection(BuddyApiCollection.Name)]
public sealed class ExportPersonalDataTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Night = new(2026, 3, 2);

    // Property names that would mean a secret leaked into the export.
    private static readonly string[] SecretProperties = ["tokenHash", "hash", "cipherText", "keycloakSubject", "emailVerification", "token"];

    [Fact]
    [CoversEndpoint("ExportPersonalData")]
    public async Task A_guardian_downloads_their_own_and_their_childrens_data_in_every_section()
    {
        var n = Unique();
        var guardian = await fixture.CreateUserAsync(givenName: $"Gerda{n}");
        var token = await fixture.GetAccessTokenAsync(guardian);
        var guardianId = await fixture.GetUserIdAsync(token);
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, $"Cosmo{n}", "Crane");

        await SleepDiaryTestHelpers.LogAsync(fixture, token, child.Id, Night, SleepDiaryTestHelpers.FullNight($"nightmare{n}"));
        var shareLink = await SleepDiaryTestHelpers.CreateShareLinkAsync(fixture, token, child.Id);
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id, new CreateMedicineScheduleOptions(Name: $"Medizor{n}"));
        await MealplanTestHelpers.CreateMealAsync(fixture, token, child.Id, new CreateMealOptions(Name: $"Meal{n}"));
        var babysitter = await BabysitterTestHelpers.AddAsync(fixture, token, $"Sitter{n}", $"sitter{n}@example.test");
        await BabysitterTestHelpers.AssignAsync(fixture, token, child.Id, Night, guardianId, babysitter.Id);
        await WorkLocationTestHelpers.AddLocationAsync(fixture, token, $"Office{n}");
        await PrintTemplateTestHelpers.CreateAsync(fixture, token, $"Template{n}");
        await GuardianTestHelpers.InviteGuardianAsync(fixture, token, child.Id, $"invitee{n}@example.test", GuardianKind.Parent);
        await SetApiKeyAsync(token, child.Id, $"sk-ant-{n}WXYZ");

        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, $"Group{n}");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, $"Calendar{n}", groupId);
        await CalendarTestHelpers.CreateEventAsync(fixture, token, calendarId, title: $"Event{n}");
        var icalToken = await CalendarTestHelpers.CreateIcalTokenAsync(fixture, token, calendarId);

        var response = await ExportAsync(token);
        var text = await response.ReadAsTextAsync();

        Assert.StartsWith("attachment;", response.Context.Response.Headers.ContentDisposition.ToString(), StringComparison.Ordinal);
        Assert.Contains("filename=buddy-export-", response.Context.Response.Headers.ContentDisposition.ToString(), StringComparison.Ordinal);

        using var json = JsonDocument.Parse(text);
        var sections = json.RootElement.GetProperty("sections");
        var expected = fixture.Host.Services.GetServices<IPersonalDataExporter>().Select(e => e.Section).Order().ToArray();
        Assert.Equal(expected, sections.EnumerateObject().Select(p => p.Name).Order().ToArray());

        Assert.Equal($"Gerda{n}", sections.GetProperty("account").GetProperty("name").GetProperty("givenName").GetString());
        Assert.Equal($"Cosmo{n}", Assert.Single(sections.GetProperty("children").GetProperty("children").EnumerateArray())
            .GetProperty("name").GetProperty("givenName").GetString());

        foreach (var value in (string[])[
            $"nightmare{n}", $"Medizor{n}", $"Meal{n}", $"Sitter{n}", $"sitter{n}@example.test", $"Office{n}",
            $"Template{n}", $"invitee{n}@example.test", $"Group{n}", $"Calendar{n}", $"Event{n}"])
        {
            Assert.Contains(value, text, StringComparison.Ordinal);
        }

        // Enums by name, for a reader without the source.
        Assert.Contains("\"role\": \"Owner\"", text, StringComparison.Ordinal);

        // The AI key's last four only; no tokens, hashes or encrypted keys.
        Assert.Contains("WXYZ", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"sk-ant-{n}", text, StringComparison.Ordinal);
        Assert.DoesNotContain(icalToken.Token, text, StringComparison.Ordinal);
        Assert.DoesNotContain(shareLink!.Token, text, StringComparison.Ordinal);
        Assert.Empty(PropertyNames(json.RootElement).Intersect(SecretProperties, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_child_downloads_only_their_own_account()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        await SleepDiaryTestHelpers.LogAsync(fixture, guardianToken, child.Id, Night, SleepDiaryTestHelpers.FullNight("child-export"));
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        using var json = JsonDocument.Parse(await (await ExportAsync(childToken)).ReadAsTextAsync());
        var sections = json.RootElement.GetProperty("sections");

        Assert.Equal(["account"], sections.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(child.Username, sections.GetProperty("account").GetProperty("userName").GetString());
    }

    [Fact]
    public async Task Another_familys_child_is_not_in_the_export()
    {
        var n = Unique();
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, otherToken, $"Stranger{n}");
        var group = await GroupTestHelpers.CreateGroupAsync(fixture, otherToken, $"OtherGroup{n}");
        await CalendarTestHelpers.CreateCalendarAsync(fixture, otherToken, $"OtherCalendar{n}", group);

        var text = await (await ExportAsync(token)).ReadAsTextAsync();

        Assert.DoesNotContain($"Stranger{n}", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"OtherGroup{n}", text, StringComparison.Ordinal);
    }

    private Task<IScenarioResult> ExportAsync(string token) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/export");
            _.StatusCodeShouldBeOk();
            _.ContentTypeShouldBe("application/json");
        });

    private Task<IScenarioResult> SetApiKeyAsync(string token, Guid childId, string apiKey) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { ApiKey = apiKey }).ToUrl($"/mealplans/children/{childId}/ai/providers/Anthropic/key");
            _.StatusCodeShouldBeOk();
        });

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => PropertyNames(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];
}
