using System.Globalization;

using buddy.Common;
using buddy.Common.Validation;

namespace buddy.Features.Mealplans;

// date;meal[;slot][;notes] -- the way in from any system that can export to a spreadsheet.
// Separator ';', ',' or tab (whichever the first data line uses); dates as yyyy-MM-dd,
// dd-MM-yyyy, dd/MM/yyyy or dd.MM.yyyy; slot as the MealSlot name (Breakfast/Lunch/Dinner/Snack,
// any case), falling back to the options' slot. A first line whose date doesn't parse is taken
// as a header and skipped. Quoted fields ("Fish, chips") are supported.
public sealed class CsvImportFormat : IMealPlanImportFormat
{
    public const string FormatId = "csv";

    public string Id => FormatId;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy"];

    public double Detect(string text)
    {
        var lines = Lines(text).Select(l => l.Text).ToList();

        if (lines.Count == 0)
        {
            return 0;
        }

        var separator = DetectSeparator(lines);
        var dated = lines.Count(line => Split(line, separator) is [var date, _, ..] && TryParseDate(date, out _));

        return (double)dated / lines.Count;
    }

    public Result<ParsedImport> Parse(string text, MealPlanImportOptions options)
    {
        var lines = Lines(text).ToList();
        var separator = DetectSeparator([.. lines.Select(l => l.Text)]);
        var warnings = new List<ImportWarning>();
        var byKey = new Dictionary<(DateOnly, MealSlot), ParsedImportLine>();
        var emptyDays = 0;

        foreach (var (index, (lineNumber, line)) in lines.Index())
        {
            var fields = Split(line, separator);

            if (fields.Count < 2 || !TryParseDate(fields[0], out var date))
            {
                if (index > 0)
                {
                    warnings.Add(new ImportWarning(lineNumber, ImportWarningCodes.UnrecognizedLine, $"Line {lineNumber} was not recognised and was ignored: \"{line}\"."));
                }

                continue;
            }

            var third = fields.Count > 2 ? fields[2].Trim() : "";
            var slot = TryParseSlot(third, out var parsedSlot) ? parsedSlot : options.Slot;
            // A third column that isn't a slot name is a note ("2025-03-14;Lasagne;extra cheese").
            var extraNotes = string.Join(" ", new[] { TryParseSlot(third, out _) ? "" : third, fields.Count > 3 ? fields[3].Trim() : "" }.Where(n => n.Length > 0));

            if (byKey.Remove((date, slot)))
            {
                warnings.Add(new ImportWarning(lineNumber, ImportWarningCodes.DuplicateDay, $"Line {lineNumber}: {date:yyyy-MM-dd} {slot} appears twice; the later line wins."));
            }

            if (ImportLineClassifier.Classify(fields[1]) is not { } classified)
            {
                emptyDays++;
                continue;
            }

            var notes = string.Join(" ", new[] { classified.Notes, extraNotes }.Where(n => n.Length > 0));

            if (notes.Length > ImportLineClassifier.MaxNotesLength)
            {
                notes = notes[..ImportLineClassifier.MaxNotesLength];
            }

            byKey[(date, slot)] = new ParsedImportLine(lineNumber, date, slot, fields[1].Trim(), classified.Kind, classified.MealName, notes, classified.Key);
        }

        if (byKey.Count == 0 && emptyDays == 0 && lines.Count > 0)
        {
            return new Result<ParsedImport>.Validation(ValidationProblem.Of("No line starts with a date followed by a meal, e.g. '2025-03-14;Lasagne'."));
        }

        return new Result<ParsedImport>.Success(new ParsedImport(
            [.. byKey.Values.OrderBy(l => l.Date).ThenBy(l => l.Slot)], warnings, emptyDays));
    }

    private static IEnumerable<(int LineNumber, string Text)> Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n')
            .Select((line, index) => (index + 1, line.Trim()))
            .Where(pair => pair.Item2.Length > 0);

    // In order of preference when a sample line holds equally many of several.
    private static readonly char[] Separators = [';', '\t', ','];

    private static char DetectSeparator(List<string> lines)
    {
        var sample = lines.Skip(lines.Count > 1 ? 1 : 0).FirstOrDefault() ?? "";

        return Separators.OrderByDescending(c => sample.Count(ch => ch == c)).First();
    }

    // Names only (Breakfast/Lunch/Dinner/Snack, any case): Enum.TryParse would also take "5" or
    // "Lunch, Dinner".
    private static bool TryParseSlot(string value, out MealSlot slot)
    {
        slot = default;

        return value.Length > 0
            && value.All(char.IsLetter)
            && Enum.TryParse(value, ignoreCase: true, out slot)
            && Enum.IsDefined(slot);
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value.Trim().Trim('"'), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    // Minimal RFC 4180: separator-delimited, double quotes around a field, "" for a literal quote.
    private static List<string> Split(string line, char separator)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == separator)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());

        return fields;
    }
}
