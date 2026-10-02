namespace buddy.Features.PrintTemplates;

// A flat discriminator rather than a union of row cases: rows are embedded in a persisted event,
// and structurally similar union cases can't be told apart on deserialization (see
// docs/backend/analysis/pickup-schedules.md, Question 3). Append new kinds at the end -- the
// ordinal is the HTTP wire value.
public enum PrintRowKind
{
    Meal,
    Pickup,
    WorkLocation,
    CalendarMarker,
    CalendarEvents,
    TaskChecklist,
    Blank
}
