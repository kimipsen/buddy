using buddy.Features.Calendars;

namespace buddy.Features.TaskLibrary;

public sealed record Subtask(SubtaskId Id, string Title, Icon? Icon, TimeSpan Duration);
