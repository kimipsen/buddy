using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Mealplans;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// GDPR Question 6.1 (docs/backend/analysis/gdpr-data-protection.md): what the assistant sends to
// the provider. No test reaches a provider, so these call the prompt builder and the tool executor
// directly.
[Collection(BuddyApiCollection.Name)]
public sealed class AiDataMinimizationTests(BuddyApiFixture fixture)
{
    [Fact]
    public void The_prompt_names_children_by_number_and_never_by_id()
    {
        // Fixed ids: two UUIDv7s made in the same millisecond don't reliably sort in creation order.
        var olderChild = new UserId(Guid.Parse("01900000-0000-7000-8000-000000000001"));
        var youngerChild = new UserId(Guid.Parse("01900000-0000-7000-8000-000000000002"));
        var ratedAt = DateTimeOffset.UtcNow;

        var pasta = NewMeal("Pasta", ImmutableDictionary<UserId, MealRating>.Empty
            .Add(youngerChild, new MealRating(2, "too spicy", ratedAt))
            .Add(olderChild, new MealRating(5, "", ratedAt)));
        var soup = NewMeal("Soup", ImmutableDictionary<UserId, MealRating>.Empty
            .Add(youngerChild, new MealRating(4, "", ratedAt)));

        var session = NewSession(DateOnly.FromDateTime(DateTime.UtcNow));
        var started = new AiSessionStarted(session.Id, olderChild, session.From, session.To, [MealSlot.Dinner], [], "", new UserId(Guid.CreateVersion7()), ratedAt);
        var prompt = AiSessionPromptBuilder.Build(session, [pasta, soup], started);

        Assert.DoesNotContain(olderChild.Value.ToString(), prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(youngerChild.Value.ToString(), prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("child 1: 5/5; child 2: 2/5 (\"too spicy\")", prompt, StringComparison.Ordinal);
        Assert.Contains("name=\"Soup\" ratings: child 2: 4/5", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Calendar_conflicts_show_titles_only_for_the_session_childs_family_items()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var (outsider, outsiderToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Robin");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

        var familyGroupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Home");
        await AddChildToGroupAsync(guardianToken, familyGroupId, child.Id);
        await AddChildToGroupAsync(guardianToken, familyGroupId, sibling.Id);
        var familyCalendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Home", familyGroupId);

        await CalendarTestHelpers.CreateEventAsync(fixture, guardianToken, familyCalendarId, "Dinner at grandma's", day);
        await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, familyCalendarId, "Football practice", day, assignedTo: child.Id);
        await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, familyCalendarId, "Dentist", day, assignedTo: sibling.Id);

        var sharedGroupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Class 3B");
        await GroupTestHelpers.AddMemberAsync(fixture, guardianToken, sharedGroupId, outsiderToken, outsider.Email, GroupRole.Member);
        var sharedCalendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Class 3B", sharedGroupId);
        await CalendarTestHelpers.CreateEventAsync(fixture, guardianToken, sharedCalendarId, "Class party", day);

        var events = await GetCalendarConflictsAsync(new UserId(guardianId), new UserId(child.Id), day);

        // The sibling's dentist task and the class calendar's party are sent as "busy".
        Assert.Equal(
            ["Dinner at grandma's", "Football practice", AiSessionToolExecutor.BusyTitle, AiSessionToolExecutor.BusyTitle],
            events.Select(e => e.Title).Order(StringComparer.Ordinal));
        Assert.All(events, e => Assert.Equal(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.Date));
        Assert.All(events, e => Assert.NotNull(e.Time));
    }

    [Fact]
    public async Task Calendar_conflicts_for_a_sibling_session_hide_the_other_childs_items()
    {
        var (_, guardianToken, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Alex");
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Robin");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

        var familyGroupId = await GroupTestHelpers.CreateGroupAsync(fixture, guardianToken, "Home");
        await AddChildToGroupAsync(guardianToken, familyGroupId, child.Id);
        await AddChildToGroupAsync(guardianToken, familyGroupId, sibling.Id);
        var familyCalendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, guardianToken, "Home", familyGroupId);
        await CalendarTestHelpers.CreateTaskAsync(fixture, guardianToken, familyCalendarId, "Football practice", day, assignedTo: child.Id);

        var events = await GetCalendarConflictsAsync(new UserId(guardianId), new UserId(sibling.Id), day);

        Assert.Equal(AiSessionToolExecutor.BusyTitle, Assert.Single(events).Title);
    }

    private sealed record ConflictDto(string Date, string? Time, string Title, bool AllDay);

    private async Task<IReadOnlyList<ConflictDto>> GetCalendarConflictsAsync(UserId callerId, UserId sessionChildId, DateOnly day)
    {
        using var scope = fixture.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var call = new AiRequestedToolCall(
            "call-1", AiSessionTools.GetCalendarConflicts, JsonSerializer.Serialize(new { from = day.ToString("O", CultureInfo.InvariantCulture), to = day.ToString("O", CultureInfo.InvariantCulture) }));

        var outcome = await AiSessionToolExecutor.ExecuteAsync(
            call,
            MealplanAiSessionId.New(),
            NewSession(day),
            [],
            callerId,
            sessionChildId,
            services.GetRequiredService<ICalendarEventStore>(),
            services.GetRequiredService<ICalendarItemEventStore>(),
            services.GetRequiredService<ITaskTemplateEventStore>(),
            services.GetRequiredService<IGroupEventStore>(),
            services.GetRequiredService<IGuardianLinkEventStore>(),
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.False(outcome.IsError, outcome.ResultJson);

        using var document = JsonDocument.Parse(outcome.ResultJson);
        return document.RootElement.GetProperty("events").Deserialize<ConflictDto[]>(JsonSerializerOptions.Web)!;
    }

    private async Task AddChildToGroupAsync(string guardianToken, Guid groupId, Guid childId) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{childId}");
            _.StatusCodeShouldBe(204);
        });

    private static MealplanAiSession NewSession(DateOnly day) => new(
        MealplanAiSessionId.New(),
        day,
        day,
        [MealSlot.Dinner],
        ImmutableDictionary<(DateOnly, MealSlot), MealId>.Empty,
        AiSessionStatus.Drafting);

    private static Meal NewMeal(string name, ImmutableDictionary<UserId, MealRating> ratings)
    {
        var guardian = new UserId(Guid.CreateVersion7());
        return new Meal(new MealId(Guid.CreateVersion7()), guardian, name, "", new Icon("🍝"), new Color("#ff0000"), false, ratings, guardian);
    }
}
