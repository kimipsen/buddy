using buddy.Common;

namespace buddy.Features.Mealplans;

// Pure parse output, before any meal-library matching -- see
// docs/backend/analysis/mealplan-import.md, Question 1.
public enum ImportLineKind
{
    Meal,
    // "Sushi / McD": the first named option becomes the meal, the full text goes to Notes.
    Alternatives,
    // "rester", "Lasagne rester" -- skipped by default.
    Leftovers,
    // "- Sommerhus", "Ingen hjemme", "Bedstefar" -- nobody cooked at home; skipped by default.
    Away
}

// Which day a week-numbered note's week starts on. Sunday: "Sø" is the day before ISO week n's
// Monday (the note this was built for plans Sunday to Saturday). Monday: plain ISO weeks.
public enum ImportWeekStart
{
    Sunday,
    Monday
}

public sealed record MealPlanImportOptions(ImportWeekStart WeekStart, MealSlot Slot)
{
    public static readonly MealPlanImportOptions Default = new(ImportWeekStart.Sunday, MealSlot.Dinner);
}

public sealed record ParsedImportLine(
    int LineNumber,
    DateOnly Date,
    MealSlot Slot,
    string RawText,
    ImportLineKind Kind,
    string MealName,
    string Notes,
    string Key);

public sealed record ImportWarning(int LineNumber, string Code, string Message);

public sealed record ParsedImport(IReadOnlyList<ParsedImportLine> Lines, IReadOnlyList<ImportWarning> Warnings, int EmptyDays);

public static class ImportWarningCodes
{
    public const string WeekNumberCorrected = "week_number_corrected";
    public const string WeekNumberInferred = "week_number_inferred";
    public const string DayInferredFromPosition = "day_inferred_from_position";
    public const string DuplicateDay = "duplicate_day";
    public const string UnrecognizedLine = "unrecognized_line";
}

public interface IMealPlanImportFormat
{
    // Stable id sent by the client and stored on MealPlanEntriesImported ("weekly-note", "csv").
    string Id { get; }

    // 0..1 confidence that text is in this format; the highest score wins auto-detection.
    double Detect(string text);

    // Validation for a text this format can't make sense of at all (e.g. weeks with no year).
    Result<ParsedImport> Parse(string text, MealPlanImportOptions options);
}

public static class MealPlanImportFormats
{
    public const string Auto = "auto";

    public static readonly IReadOnlyList<IMealPlanImportFormat> All = [new WeeklyNoteImportFormat(), new CsvImportFormat()];

    public static IMealPlanImportFormat? Find(string id) =>
        All.FirstOrDefault(format => string.Equals(format.Id, id, StringComparison.OrdinalIgnoreCase));

    // Below this score nothing is a convincing match, and the guardian is asked to pick a format.
    private const double MinimumConfidence = 0.3;

    public static IMealPlanImportFormat? Detect(string text)
    {
        var (format, score) = All.Select(f => (f, f.Detect(text))).MaxBy(pair => pair.Item2);

        return score >= MinimumConfidence ? format : null;
    }
}
