using buddy.Features.Calendars;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.Features.Calendars;

// Pure-function coverage for CalendarOccurrenceExpansion's template-scheduled-task branch --
// no HTTP/DB involved (same "no fixture needed" shape as EventShapeTests), just fakes for the two
// event stores it reads from, so the exact subtask-boundary math (including the DST case, where
// getting it wrong by resolving one anchor instant and adding TimeSpans would be a full hour off)
// can be asserted precisely instead of only observed through a full API round-trip.
public sealed class CalendarOccurrenceExpansionTests
{
    private static readonly CalendarId FixedCalendarId = new(Guid.CreateVersion7());
    private static readonly CalendarItemId FixedItemId = new(Guid.CreateVersion7());
    private static readonly TaskTemplateId FixedTemplateId = new(Guid.CreateVersion7());
    private static readonly UserId FixedUserId = new(Guid.CreateVersion7());
    private static readonly TimeZoneId Copenhagen = TimeZoneId.New("Europe/Copenhagen");
    private static readonly Icon CalendarIcon = Icon.New("calendar");

    private static CalendarItemEvent TaskCreated(DueDate dueDate, RecurrenceRule? recurrence = null) =>
        new TaskItemCreated(FixedItemId, FixedCalendarId, FixedUserId, "Morning routine", null, Color.New("#ff0000"), dueDate, recurrence, DateTimeOffset.UtcNow, null, FixedTemplateId.Value);

    private static TaskTemplateEvent TemplateCreated() =>
        new TaskTemplateCreated(FixedTemplateId, FixedUserId, FixedUserId, "Morning routine", Icon.New("sunrise"), Color.New("#ffaa00"), DateTimeOffset.UtcNow);

    private static TaskTemplateEvent SubtaskAdded(string title, TimeSpan duration) =>
        new SubtaskAdded(FixedTemplateId, new Subtask(SubtaskId.New(), title, null, duration), int.MaxValue, FixedUserId, DateTimeOffset.UtcNow);

    [Fact]
    public async Task A_daily_template_task_expands_into_back_to_back_subtask_occurrences()
    {
        var due = new DueDate(new DateOnly(2026, 6, 1), new TimeOnly(7, 0));
        var items = new FakeCalendarItemEventStore().Add(FixedItemId, TaskCreated(due, new RecurrenceRule(RecurrenceFrequency.Daily, 1, null)));

        var subtask1 = SubtaskAdded("Brush teeth", TimeSpan.FromMinutes(10));
        var subtask2 = SubtaskAdded("Get dressed", TimeSpan.FromMinutes(15));
        var templates = new FakeTaskTemplateEventStore().Add(FixedTemplateId, TemplateCreated(), subtask1, subtask2);

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(
            FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None);

        var ordered = occurrences.OrderBy(o => o.StartsAt).ToArray();
        Assert.Equal(2, ordered.Length);

        Assert.Equal("Brush teeth", ordered[0].Title);
        Assert.Equal("Morning routine", ordered[0].ParentTitle);
        Assert.Equal(TimeSpan.FromMinutes(10), ordered[0].EndsAt!.Value - ordered[0].StartsAt!.Value);

        Assert.Equal("Get dressed", ordered[1].Title);
        Assert.Equal(ordered[0].EndsAt, ordered[1].StartsAt);
        Assert.Equal(TimeSpan.FromMinutes(15), ordered[1].EndsAt!.Value - ordered[1].StartsAt!.Value);

        // DueAt mirrors StartsAt, same as a plain task's occurrence.
        Assert.Equal(ordered[0].StartsAt, ordered[0].DueAt);
    }

    // ParentIcon must read off the parent item (falling back to the calendar), never the
    // subtask's own icon -- a routine's subtasks can each carry a distinct icon (e.g. a toothbrush
    // for "brush teeth"), which would be the wrong value for the group's own header.
    [Fact]
    public async Task ParentIcon_is_the_items_own_icon_not_the_subtasks()
    {
        var due = new DueDate(new DateOnly(2026, 6, 1), new TimeOnly(7, 0));
        var itemCreated = new TaskItemCreated(
            FixedItemId, FixedCalendarId, FixedUserId, "Morning routine", Icon.New("moon"), Color.New("#ff0000"), due, null, DateTimeOffset.UtcNow, null, FixedTemplateId.Value);
        var items = new FakeCalendarItemEventStore().Add(FixedItemId, itemCreated);

        var subtask = new SubtaskAdded(FixedTemplateId, new Subtask(SubtaskId.New(), "Brush teeth", Icon.New("toothbrush"), TimeSpan.FromMinutes(10)), int.MaxValue, FixedUserId, DateTimeOffset.UtcNow);
        var templates = new FakeTaskTemplateEventStore().Add(FixedTemplateId, TemplateCreated(), subtask);

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(
            FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None);

        var occurrence = Assert.Single(occurrences);
        Assert.Equal("toothbrush", occurrence.Icon);
        Assert.Equal("moon", occurrence.ParentIcon);
    }

    [Fact]
    public async Task ParentIcon_falls_back_to_the_calendars_icon_when_the_item_has_no_override()
    {
        var due = new DueDate(new DateOnly(2026, 6, 1), new TimeOnly(7, 0));
        var items = new FakeCalendarItemEventStore().Add(FixedItemId, TaskCreated(due));
        var templates = new FakeTaskTemplateEventStore().Add(FixedTemplateId, TemplateCreated(), SubtaskAdded("Brush teeth", TimeSpan.FromMinutes(10)));

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(
            FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None);

        Assert.Equal(CalendarIcon.Value, Assert.Single(occurrences).ParentIcon);
    }

    [Fact]
    public async Task Editing_a_subtasks_duration_changes_future_expansion_without_re_scheduling()
    {
        var due = new DueDate(new DateOnly(2026, 6, 1), new TimeOnly(7, 0));
        var items = new FakeCalendarItemEventStore().Add(FixedItemId, TaskCreated(due));

        var subtaskId = SubtaskId.New();
        var added = new SubtaskAdded(FixedTemplateId, new Subtask(subtaskId, "Brush teeth", null, TimeSpan.FromMinutes(10)), 0, FixedUserId, DateTimeOffset.UtcNow);
        var templates = new FakeTaskTemplateEventStore().Add(FixedTemplateId, TemplateCreated(), added);

        var before = await CalendarOccurrenceExpansion.ExpandAsync(FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None);
        Assert.Equal(TimeSpan.FromMinutes(10), Assert.Single(before).EndsAt!.Value - Assert.Single(before).StartsAt!.Value);

        // No new CalendarItem event at all -- only the template changes.
        var updated = new SubtaskUpdated(FixedTemplateId, subtaskId, added.Subtask, added.Subtask with { Duration = TimeSpan.FromMinutes(20) }, FixedUserId, DateTimeOffset.UtcNow);
        templates.Add(FixedTemplateId, TemplateCreated(), added, updated);

        var after = await CalendarOccurrenceExpansion.ExpandAsync(FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None);
        var afterOccurrence = Assert.Single(after);
        Assert.Equal(TimeSpan.FromMinutes(20), afterOccurrence.EndsAt!.Value - afterOccurrence.StartsAt!.Value);
    }

    [Fact]
    public async Task An_archived_templates_already_scheduled_item_still_expands_normally()
    {
        var due = new DueDate(new DateOnly(2026, 6, 1), new TimeOnly(7, 0));
        var items = new FakeCalendarItemEventStore().Add(FixedItemId, TaskCreated(due));

        var archived = new TaskTemplateArchived(FixedTemplateId, FixedUserId, DateTimeOffset.UtcNow);
        var templates = new FakeTaskTemplateEventStore().Add(FixedTemplateId, TemplateCreated(), SubtaskAdded("Brush teeth", TimeSpan.FromMinutes(10)), archived);

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None);

        Assert.Single(occurrences);
    }

    [Fact]
    public async Task A_missing_template_expands_to_zero_occurrences_without_throwing()
    {
        var due = new DueDate(new DateOnly(2026, 6, 1), new TimeOnly(7, 0));
        var items = new FakeCalendarItemEventStore().Add(FixedItemId, TaskCreated(due));
        var templates = new FakeTaskTemplateEventStore(); // no stream for FixedTemplateId at all

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None);

        Assert.Empty(occurrences);
    }

    // Europe/Copenhagen springs forward from 02:00 CET (+1) to 03:00 CEST (+2) on the last Sunday
    // of March -- 2026-03-29. A routine starting at 01:00 with a 30-minute first subtask and a
    // 90-minute second subtask has its second subtask's wall-clock window (01:30 -> 03:00) straddle
    // that gap. Resolving each boundary independently (the correct approach) yields a 30-minute
    // real-time gap for that subtask, since the clock skipped an hour partway through; resolving
    // the anchor once and adding TimeSpans (the wrong approach this test guards against) would
    // instead produce a 90-minute UTC gap, an hour too long.
    [Fact]
    public async Task A_subtask_straddling_a_dst_transition_computes_correct_boundary_times()
    {
        var due = new DueDate(new DateOnly(2026, 3, 29), new TimeOnly(1, 0));
        var items = new FakeCalendarItemEventStore().Add(FixedItemId, TaskCreated(due));

        var templates = new FakeTaskTemplateEventStore().Add(
            FixedTemplateId,
            TemplateCreated(),
            SubtaskAdded("Before the gap", TimeSpan.FromMinutes(30)),
            SubtaskAdded("Straddles the gap", TimeSpan.FromMinutes(90)),
            SubtaskAdded("After the gap", TimeSpan.FromMinutes(30)));

        var occurrences = (await CalendarOccurrenceExpansion.ExpandAsync(
            FixedCalendarId, Copenhagen, CalendarIcon, due.Date, due.Date, items, templates, CancellationToken.None))
            .OrderBy(o => o.StartsAt)
            .ToArray();

        Assert.Equal(3, occurrences.Length);

        var expectedStart = new DateTimeOffset(2026, 3, 29, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedStart, occurrences[0].StartsAt);
        Assert.Equal(expectedStart.AddMinutes(30), occurrences[0].EndsAt);

        Assert.Equal(expectedStart.AddMinutes(30), occurrences[1].StartsAt);
        // The correct, DST-aware boundary -- 30 real minutes elapsed, not the naive 90.
        Assert.Equal(expectedStart.AddMinutes(60), occurrences[1].EndsAt);

        Assert.Equal(expectedStart.AddMinutes(60), occurrences[2].StartsAt);
        Assert.Equal(expectedStart.AddMinutes(90), occurrences[2].EndsAt);
    }
}

internal sealed class FakeCalendarItemEventStore : ICalendarItemEventStore
{
    private readonly Dictionary<CalendarItemId, List<CalendarItemEvent>> _streams = [];

    public FakeCalendarItemEventStore Add(CalendarItemId id, params CalendarItemEvent[] events)
    {
        _streams[id] = [.. events];
        return this;
    }

    public Task<IReadOnlyCollection<CalendarItemEvent>> ReadAsync(CalendarItemId itemId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<CalendarItemEvent>>(_streams.TryGetValue(itemId, out var events) ? events : []);

    public Task<IReadOnlyCollection<CalendarItemEvent>> CreateAsync(CalendarItemId itemId, IReadOnlyCollection<CalendarItemEvent> events, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task AppendAsync(CalendarItemId itemId, IReadOnlyCollection<CalendarItemEvent> events, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyCollection<CalendarItemId>> ListIdsForCalendarAsync(CalendarId calendarId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<CalendarItemId>>([.. _streams.Keys]);
}

internal sealed class FakeTaskTemplateEventStore : ITaskTemplateEventStore
{
    private readonly Dictionary<TaskTemplateId, List<TaskTemplateEvent>> _streams = [];

    public FakeTaskTemplateEventStore Add(TaskTemplateId id, params TaskTemplateEvent[] events)
    {
        _streams[id] = [.. events];
        return this;
    }

    public Task<IReadOnlyCollection<TaskTemplateEvent>> ReadAsync(TaskTemplateId id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<TaskTemplateEvent>>(_streams.TryGetValue(id, out var events) ? events : []);

    public Task<IReadOnlyCollection<TaskTemplateEvent>> CreateAsync(TaskTemplateId id, IReadOnlyCollection<TaskTemplateEvent> events, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task AppendAsync(TaskTemplateId id, IReadOnlyCollection<TaskTemplateEvent> events, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyCollection<TaskTemplateId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<UserId?> FindChildIdForTemplateAsync(TaskTemplateId id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
