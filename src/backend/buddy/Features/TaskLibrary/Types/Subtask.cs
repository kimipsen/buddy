using System.Text.Json.Serialization;

using buddy.Features.Calendars;

namespace buddy.Features.TaskLibrary;

// Weekdays is None ("whenever the template occurs") for every routine step and for a checklist
// item that applies every day; otherwise the item only expands on the named days. Left out of the
// stored JSON while None, so SubtaskAdded/SubtaskUpdated events written before per-item weekdays
// keep their exact shape (docs/backend/analysis/packing-checklists.md, Question 3).
public sealed record Subtask(
    SubtaskId Id,
    string Title,
    Icon? Icon,
    TimeSpan Duration,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] Weekdays Weekdays = Weekdays.None)
{
    public bool AppliesOn(DateOnly date) => Weekdays == Weekdays.None || Weekdays.Includes(date);
}
