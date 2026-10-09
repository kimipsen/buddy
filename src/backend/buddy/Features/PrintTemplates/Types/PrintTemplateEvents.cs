using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.PrintTemplates;

public union PrintTemplateEvent(
    PrintTemplateCreated,
    PrintTemplateCreatedForGroup,
    PrintTemplateRenamed,
    PrintTemplateLayoutChanged,
    PrintTemplateRowsReplaced,
    PrintTemplateGuardianColorsReplaced,
    PrintTemplateBabysitterColorsReplaced,
    PrintTemplateDeleted
)
{
    public static PrintTemplateEvent FromPayload(object payload) => payload switch
    {
        PrintTemplateCreated e => e,
        PrintTemplateCreatedForGroup e => e,
        PrintTemplateRenamed e => e,
        PrintTemplateLayoutChanged e => e,
        PrintTemplateRowsReplaced e => e,
        PrintTemplateGuardianColorsReplaced e => e,
        PrintTemplateBabysitterColorsReplaced e => e,
        PrintTemplateDeleted e => e,
        _ => throw new ArgumentException($"Unknown print template event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        PrintTemplateCreated => nameof(PrintTemplateCreated),
        PrintTemplateCreatedForGroup => nameof(PrintTemplateCreatedForGroup),
        PrintTemplateRenamed => nameof(PrintTemplateRenamed),
        PrintTemplateLayoutChanged => nameof(PrintTemplateLayoutChanged),
        PrintTemplateRowsReplaced => nameof(PrintTemplateRowsReplaced),
        PrintTemplateGuardianColorsReplaced => nameof(PrintTemplateGuardianColorsReplaced),
        PrintTemplateBabysitterColorsReplaced => nameof(PrintTemplateBabysitterColorsReplaced),
        PrintTemplateDeleted => nameof(PrintTemplateDeleted),
    };
}

// Two sibling creation events rather than one with an owner union inside, keeping every
// persisted event union-free.
public sealed record PrintTemplateCreated(PrintTemplateId Id, UserId OwnerId, string Name, UserId CreatedBy, DateTimeOffset OccurredAt);

public sealed record PrintTemplateCreatedForGroup(PrintTemplateId Id, GroupId OwnerId, string Name, UserId CreatedBy, DateTimeOffset OccurredAt);

public sealed record PrintTemplateRenamed(PrintTemplateId Id, string Before, string After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record PrintTemplateLayoutChanged(
    PrintTemplateId Id,
    PaperSize PaperSizeBefore,
    PaperSize PaperSizeAfter,
    DayOfWeek StartWeekdayBefore,
    DayOfWeek StartWeekdayAfter,
    bool ShowWeekNumberBefore,
    bool ShowWeekNumberAfter,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);

// Whole-list replacement: the editor edits the list as a whole (add, remove, reorder), and one
// event keeps every edit atomic. Templates hold at most 12 rows.
public sealed record PrintTemplateRowsReplaced(
    PrintTemplateId Id,
    IReadOnlyList<PrintTemplateRow> Before,
    IReadOnlyList<PrintTemplateRow> After,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);

public sealed record PrintTemplateGuardianColorsReplaced(
    PrintTemplateId Id,
    IReadOnlyList<GuardianColor> Before,
    IReadOnlyList<GuardianColor> After,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);

public sealed record PrintTemplateBabysitterColorsReplaced(
    PrintTemplateId Id,
    IReadOnlyList<BabysitterColor> Before,
    IReadOnlyList<BabysitterColor> After,
    UserId ModifiedBy,
    DateTimeOffset OccurredAt);

public sealed record PrintTemplateDeleted(PrintTemplateId Id, UserId DeletedBy, DateTimeOffset OccurredAt);
