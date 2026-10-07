using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

using buddy.Common;
using buddy.Common.Validation;

namespace buddy.Features.Mealplans;

// A note organised as year sections, ISO week headers and one line per day:
//
//   Madplan 2026
//   U2
//   Sø: Fiskefrikadeller m salat
//   Ma: Kyllingeburger 🍔
//   ...
//
// Year sections may come in any order (newest first is common). Week headers: "U2", "Uge 2",
// "W2", "Week 2", with or without a trailing ':'. Day names: Danish or English, abbreviated or
// full, any case. The recovery rules for typos are in docs/backend/analysis/mealplan-import.md,
// Question 7; every one of them leaves a warning with its line number.
public sealed partial class WeeklyNoteImportFormat : IMealPlanImportFormat
{
    public const string FormatId = "weekly-note";

    public string Id => FormatId;

    // Day names -> ISO day of week (Monday = 1 ... Sunday = 7), one line per day on purpose.
#pragma warning disable IDE0055 // Formatting would put each of the 44 entries on its own line.
    private static readonly Dictionary<string, int> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mandag"] = 1, ["man"] = 1, ["ma"] = 1, ["monday"] = 1, ["mon"] = 1, ["mo"] = 1,
        ["tirsdag"] = 2, ["tirs"] = 2, ["tir"] = 2, ["ti"] = 2, ["tuesday"] = 2, ["tues"] = 2, ["tue"] = 2, ["tu"] = 2,
        ["onsdag"] = 3, ["ons"] = 3, ["on"] = 3, ["wednesday"] = 3, ["wed"] = 3, ["we"] = 3,
        ["torsdag"] = 4, ["tors"] = 4, ["tor"] = 4, ["to"] = 4, ["thursday"] = 4, ["thurs"] = 4, ["thur"] = 4, ["thu"] = 4, ["th"] = 4,
        ["fredag"] = 5, ["fre"] = 5, ["fr"] = 5, ["friday"] = 5, ["fri"] = 5,
        ["lørdag"] = 6, ["lør"] = 6, ["lø"] = 6, ["saturday"] = 6, ["sat"] = 6, ["sa"] = 6,
        ["søndag"] = 7, ["søn"] = 7, ["sø"] = 7, ["sunday"] = 7, ["sun"] = 7, ["su"] = 7,
    };
#pragma warning restore IDE0055

    public double Detect(string text)
    {
        var lines = NonEmptyLines(text).ToList();

        if (lines.Count == 0)
        {
            return 0;
        }

        var recognised = lines.Count(line => YearHeader().IsMatch(line) || WeekHeader().IsMatch(line) || TryDayLine(line, out _, out _));

        return (double)recognised / lines.Count;
    }

    public Result<ParsedImport> Parse(string text, MealPlanImportOptions options)
    {
        var parser = new Parser(options);
        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            if (parser.Accept(index + 1, lines[index].Trim()) is { } problem)
            {
                return new Result<ParsedImport>.Validation(problem);
            }
        }

        return new Result<ParsedImport>.Success(parser.Finish());
    }

    private static IEnumerable<string> NonEmptyLines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);

    private static bool TryDayLine(string line, out int isoDay, out string text)
    {
        var match = DayLine().Match(line);
        isoDay = 0;
        text = "";

        if (!match.Success || !DayNames.TryGetValue(match.Groups["day"].Value, out isoDay))
        {
            return false;
        }

        text = match.Groups["text"].Value.Trim();
        return true;
    }

    // Line-by-line state machine. Day lines that come before any week header in a year section
    // are held in `pending` until the next week header tells us their week (header - 1).
    private sealed class Parser(MealPlanImportOptions options)
    {
        private readonly List<ImportWarning> _warnings = [];
        private readonly Dictionary<DateOnly, ParsedImportLine> _byDate = [];
        private readonly List<(int LineNumber, int Position, string Text, bool Inferred)> _pending = [];
        private int _emptyDays;
        private int? _year;
        private int? _week;
        private int _previousWeek;
        // Position (0..6) within the week, in the order the note lists days, of the last day line.
        private int _lastPosition = -1;

        public ValidationProblem? Accept(int lineNumber, string line)
        {
            if (line.Length == 0)
            {
                return null;
            }

            if (YearHeader().Match(line) is { Success: true } yearMatch)
            {
                FlushPendingWithoutWeek();
                _year = int.Parse(yearMatch.Groups["year"].Value, CultureInfo.InvariantCulture);
                _week = null;
                _previousWeek = 0;
                _lastPosition = -1;
                return null;
            }

            if (WeekHeader().Match(line) is { Success: true } weekMatch)
            {
                if (_year is null)
                {
                    return ValidationProblem.Of($"Line {lineNumber}: a week comes before any year. Add a year line such as 'Madplan {DateTime.UtcNow.Year}' above it.");
                }

                StartWeek(lineNumber, int.Parse(weekMatch.Groups["week"].Value, CultureInfo.InvariantCulture));
                return null;
            }

            int position;
            string text;
            var inferred = false;

            if (TryDayLine(line, out var isoDay, out var dayText))
            {
                position = PositionOf(isoDay);
                text = dayText;
            }
            else if (_year is not null && _lastPosition < 6 && (_week is not null || _pending.Count > 0))
            {
                // A line with no day name inside a week: the day after the previous line.
                position = _lastPosition + 1;
                text = line;
                inferred = true;
            }
            else
            {
                _warnings.Add(new ImportWarning(lineNumber, ImportWarningCodes.UnrecognizedLine, $"Line {lineNumber} was not recognised and was ignored: \"{line}\"."));
                return null;
            }

            if (_year is null)
            {
                return ValidationProblem.Of($"Line {lineNumber}: a day comes before any year. Add a year line such as 'Madplan {DateTime.UtcNow.Year}' above it.");
            }

            _lastPosition = position;

            if (_week is null)
            {
                _pending.Add((lineNumber, position, text, inferred));
            }
            else
            {
                AddDay(lineNumber, _year.Value, _week.Value, position, text, inferred);
            }

            return null;
        }

        public ParsedImport Finish()
        {
            FlushPendingWithoutWeek();

            return new ParsedImport([.. _byDate.Values.OrderBy(l => l.Date).ThenBy(l => l.LineNumber)], _warnings, _emptyDays);
        }

        private void StartWeek(int lineNumber, int number)
        {
            var maxWeek = ISOWeek.GetWeeksInYear(_year!.Value);
            var corrected = number;

            if (number < 1 || number > maxWeek || number <= _previousWeek)
            {
                corrected = Math.Min(_previousWeek + 1, maxWeek);
                _warnings.Add(new ImportWarning(lineNumber, ImportWarningCodes.WeekNumberCorrected,
                    $"Line {lineNumber}: week {number} doesn't follow week {_previousWeek}; read as week {corrected}."));
            }

            if (_pending.Count > 0)
            {
                // The week before week 1 is the previous year's last ISO week.
                var (inferredYear, inferredWeek) = corrected > 1
                    ? (_year.Value, corrected - 1)
                    : (_year.Value - 1, ISOWeek.GetWeeksInYear(_year.Value - 1));
                _warnings.Add(new ImportWarning(_pending[0].LineNumber, ImportWarningCodes.WeekNumberInferred,
                    $"Line {_pending[0].LineNumber}: days with no week header above them were read as week {inferredWeek} of {inferredYear}."));

                foreach (var (pendingLine, position, text, inferred) in _pending)
                {
                    AddDay(pendingLine, inferredYear, inferredWeek, position, text, inferred);
                }

                _pending.Clear();
            }

            _week = corrected;
            _previousWeek = corrected;
            _lastPosition = -1;
        }

        private void FlushPendingWithoutWeek()
        {
            foreach (var (lineNumber, _, text, _) in _pending)
            {
                _warnings.Add(new ImportWarning(lineNumber, ImportWarningCodes.UnrecognizedLine,
                    $"Line {lineNumber} is not under any week and was ignored: \"{text}\"."));
            }

            _pending.Clear();
        }

        private void AddDay(int lineNumber, int dayYear, int dayWeek, int position, string text, bool inferred)
        {
            var monday = DateOnly.FromDateTime(ISOWeek.ToDateTime(dayYear, dayWeek, DayOfWeek.Monday));
            var date = monday.AddDays(OffsetFromMonday(position));

            if (inferred)
            {
                _warnings.Add(new ImportWarning(lineNumber, ImportWarningCodes.DayInferredFromPosition,
                    $"Line {lineNumber} has no day name; read as {date:yyyy-MM-dd} from its position in the week."));
            }

            if (_byDate.Remove(date))
            {
                _warnings.Add(new ImportWarning(lineNumber, ImportWarningCodes.DuplicateDay,
                    $"Line {lineNumber}: {date:yyyy-MM-dd} appears twice; the later line wins."));
            }

            if (ImportLineClassifier.Classify(text) is not { } classified)
            {
                _emptyDays++;
                return;
            }

            _byDate[date] = new ParsedImportLine(lineNumber, date, options.Slot, text, classified.Kind, classified.MealName, classified.Notes, classified.Key);
        }

        // Position in the note's own day order (0 = the week's first listed day).
        private int PositionOf(int isoDay) => options.WeekStart switch
        {
            ImportWeekStart.Sunday => isoDay % 7,      // Sunday 7 -> 0, Monday 1 -> 1, ..., Saturday 6 -> 6
            ImportWeekStart.Monday => isoDay - 1,      // Monday 1 -> 0, ..., Sunday 7 -> 6
            _ => throw new UnreachableException($"Unrecognized ImportWeekStart value: {options.WeekStart}."),
        };

        private int OffsetFromMonday(int position) => options.WeekStart switch
        {
            ImportWeekStart.Sunday => position - 1,    // Sunday is the day before ISO Monday
            ImportWeekStart.Monday => position,
            _ => throw new UnreachableException($"Unrecognized ImportWeekStart value: {options.WeekStart}."),
        };
    }

    [GeneratedRegex(@"^(?:(?:madplan|meal\s*plan|mealplan)\s*)?(?<year>(?:19|20)\d{2})\s*:?$", RegexOptions.IgnoreCase)]
    private static partial Regex YearHeader();

    [GeneratedRegex(@"^(?:u|uge|w|wk|week)\s*\.?\s*(?<week>\d{1,3})\s*:?$", RegexOptions.IgnoreCase)]
    private static partial Regex WeekHeader();

    [GeneratedRegex(@"^(?<day>\p{L}{2,9})\s*:\s*(?<text>.*)$")]
    private static partial Regex DayLine();
}
