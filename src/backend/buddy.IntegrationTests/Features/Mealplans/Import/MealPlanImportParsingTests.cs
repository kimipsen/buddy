using System.Collections.Immutable;

using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Mealplans;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Import;

// Pure parser/matcher tests -- no containers. The fixtures are made up, in the shape of a real
// family's phone note (see docs/backend/analysis/mealplan-import.md, Context): newest year first,
// Sunday-first weeks, typos in week headers, a headerless week, a week without day names.
public sealed class MealPlanImportParsingTests
{
    private const string Note = """
        Madplan 2026
        U2
        Sø: Fiskefrikadeller m salat 🐟
        Ma: Kyllingeburger 🍔
        Ti:
        On: Rugbrød m fisk
        To: Rester
        Fr: Sushi / McD 🍱
        Lø: - Sommerhus

        U66
        Sø: Lasagne (Mor ikke hjemme)
        ma: Pølser og pasta + GS

        Madplan 2025
        U1
        Søndag: Risengrød

        U42:
        Nakkefilet m. ovnkartofler
        Forårsruller m. salatbar
        Nachos

        Madplan 2024
        Sø: Rejespyd
        Ma: Tarteletter
        U3
        Sø: Boller i karry
        """;

    private static ParsedImport ParseNote(string text, MealPlanImportOptions? options = null) =>
        new WeeklyNoteImportFormat().Parse(text, options ?? MealPlanImportOptions.Default) is Result<ParsedImport>.Success(var parsed)
            ? parsed
            : throw new Xunit.Sdk.XunitException("Expected the note to parse.");

    private static ParsedImportLine At(ParsedImport parsed, int year, int month, int day) =>
        Assert.Single(parsed.Lines, l => l.Date == new DateOnly(year, month, day));

    [Fact]
    public void A_Sunday_first_week_puts_Sunday_the_day_before_the_ISO_weeks_Monday()
    {
        var parsed = ParseNote(Note);

        // ISO week 2 of 2026 starts Monday 5 January.
        Assert.Equal("Fiskefrikadeller m salat", At(parsed, 2026, 1, 4).MealName);
        Assert.Equal("Kyllingeburger", At(parsed, 2026, 1, 5).MealName);
        Assert.Equal("Rugbrød m fisk", At(parsed, 2026, 1, 7).MealName);
        Assert.All(parsed.Lines, l => Assert.Equal(MealSlot.Dinner, l.Slot));
    }

    [Fact]
    public void A_Monday_first_week_puts_Sunday_at_the_end()
    {
        var parsed = ParseNote("2026\nU2\nMa: Burger\nSø: Pizza", new MealPlanImportOptions(ImportWeekStart.Monday, MealSlot.Lunch));

        Assert.Equal("Burger", At(parsed, 2026, 1, 5).MealName);
        Assert.Equal("Pizza", At(parsed, 2026, 1, 11).MealName);
        Assert.All(parsed.Lines, l => Assert.Equal(MealSlot.Lunch, l.Slot));
    }

    [Fact]
    public void Week_one_crosses_into_the_previous_year()
    {
        var parsed = ParseNote(Note);

        Assert.Equal("Risengrød", At(parsed, 2024, 12, 29).MealName);
    }

    [Fact]
    public void An_impossible_week_number_is_corrected_to_the_next_week_with_a_warning()
    {
        var parsed = ParseNote(Note);

        Assert.Equal("Lasagne", At(parsed, 2026, 1, 11).MealName);
        Assert.Contains(parsed.Warnings, w => w.Code == ImportWarningCodes.WeekNumberCorrected && w.LineNumber == 11);
    }

    [Fact]
    public void Days_without_a_week_header_take_the_week_before_the_next_header()
    {
        var parsed = ParseNote(Note);

        Assert.Equal("Rejespyd", At(parsed, 2024, 1, 7).MealName);
        Assert.Equal("Tarteletter", At(parsed, 2024, 1, 8).MealName);
        Assert.Equal("Boller i karry", At(parsed, 2024, 1, 14).MealName);
        Assert.Contains(parsed.Warnings, w => w.Code == ImportWarningCodes.WeekNumberInferred);
    }

    [Fact]
    public void Lines_without_a_day_name_follow_each_other_from_the_weeks_first_day()
    {
        var parsed = ParseNote(Note);

        Assert.Equal("Nakkefilet m. ovnkartofler", At(parsed, 2025, 10, 12).MealName);
        Assert.Equal("Forårsruller m. salatbar", At(parsed, 2025, 10, 13).MealName);
        Assert.Equal("Nachos", At(parsed, 2025, 10, 14).MealName);
        Assert.Equal(3, parsed.Warnings.Count(w => w.Code == ImportWarningCodes.DayInferredFromPosition));
    }

    [Fact]
    public void Empty_days_are_counted_not_imported()
    {
        var parsed = ParseNote(Note);

        Assert.DoesNotContain(parsed.Lines, l => l.Date == new DateOnly(2026, 1, 6));
        Assert.Equal(1, parsed.EmptyDays);
    }

    [Fact]
    public void A_repeated_day_keeps_the_later_line_with_a_warning()
    {
        var parsed = ParseNote("2026\nU2\nMa: Burger\nMa: Pizza");

        Assert.Equal("Pizza", At(parsed, 2026, 1, 5).MealName);
        Assert.Contains(parsed.Warnings, w => w.Code == ImportWarningCodes.DuplicateDay && w.LineNumber == 4);
    }

    [Fact]
    public void A_week_before_any_year_is_rejected()
    {
        var result = new WeeklyNoteImportFormat().Parse("U2\nMa: Burger", MealPlanImportOptions.Default);

        Assert.True(result is Result<ParsedImport>.Validation);
    }

    [Fact]
    public void Lines_are_classified_and_side_notes_move_to_notes()
    {
        var parsed = ParseNote(Note);

        Assert.Equal(ImportLineKind.Leftovers, At(parsed, 2026, 1, 8).Kind);
        Assert.Equal(ImportLineKind.Away, At(parsed, 2026, 1, 10).Kind);

        var alternatives = At(parsed, 2026, 1, 9);
        Assert.Equal((ImportLineKind.Alternatives, "Sushi", "Sushi / McD"), (alternatives.Kind, alternatives.MealName, alternatives.Notes));

        var lasagne = At(parsed, 2026, 1, 11);
        Assert.Equal((ImportLineKind.Meal, "Lasagne", "Mor ikke hjemme"), (lasagne.Kind, lasagne.MealName, lasagne.Notes));

        var pasta = At(parsed, 2026, 1, 12);
        Assert.Equal(("Pølser og pasta", "+ GS"), (pasta.MealName, pasta.Notes));
    }

    [Theory]
    [InlineData("Bagels m/ laks 🍣", ImportLineKind.Meal, "Bagels m. laks", "")]
    [InlineData("Nachos - Sally ikke hjemme", ImportLineKind.Meal, "Nachos", "Sally ikke hjemme")]
    [InlineData("Tomatsuppe -> Pizza (Valentino)", ImportLineKind.Meal, "Pizza", "Tomatsuppe -> Pizza Valentino")]
    [InlineData("Butterchicken m. ris/blomkålsris", ImportLineKind.Meal, "Butterchicken m. ris/blomkålsris", "")]
    [InlineData("Ingen hjemme", ImportLineKind.Away, "Ingen hjemme", "")]
    [InlineData("i Silkeborg", ImportLineKind.Away, "I Silkeborg", "")]
    [InlineData("Koldskål / rester", ImportLineKind.Alternatives, "Koldskål", "Koldskål / rester")]
    [InlineData("rester 🍝/ 🍕", ImportLineKind.Leftovers, "Rester", "")]
    [InlineData("rugbrød m fisk", ImportLineKind.Meal, "Rugbrød m fisk", "")]
    [InlineData("Mormors frikadeller", ImportLineKind.Meal, "Mormors frikadeller", "")]
    [InlineData("Feriepizza", ImportLineKind.Meal, "Feriepizza", "")]
    [InlineData("Hos mormor", ImportLineKind.Away, "Hos mormor", "")]
    public void The_classifier_handles_note_shorthand(string raw, ImportLineKind kind, string name, string notes)
    {
        var classified = ImportLineClassifier.Classify(raw);

        Assert.NotNull(classified);
        Assert.Equal((kind, name, notes), (classified.Kind, classified.MealName, classified.Notes));
    }

    [Fact]
    public void Text_that_is_only_emojis_or_notes_is_an_empty_day()
    {
        Assert.Null(ImportLineClassifier.Classify("🍕 🍔"));
        Assert.Null(ImportLineClassifier.Classify("(Mor ikke hjemme)"));
    }

    [Fact]
    public void Spelling_variants_of_with_share_one_key()
    {
        var keys = new[] { "Rugbrød m fisk 🐟", "rugbrød med fisk", "Rugbrød m. fisk" }.Select(ImportLineClassifier.NormalizeKey).Distinct();

        Assert.Equal("rugbrød m fisk", Assert.Single(keys));
    }

    [Fact]
    public void Csv_rows_parse_with_optional_slot_and_notes_and_a_header()
    {
        const string csv = """
            date;meal;slot;notes
            2025-03-14;Lasagne;;extra cheese
            15-03-2025;"Fish; chips";Lunch
            2025-03-16;
            not a date;Pizza
            """;

        var result = new CsvImportFormat().Parse(csv, MealPlanImportOptions.Default);
        var parsed = result is Result<ParsedImport>.Success(var p) ? p : throw new Xunit.Sdk.XunitException("Expected the CSV to parse.");

        Assert.Equal(2, parsed.Lines.Count);
        Assert.Equal((new DateOnly(2025, 3, 14), MealSlot.Dinner, "Lasagne", "extra cheese"), (parsed.Lines[0].Date, parsed.Lines[0].Slot, parsed.Lines[0].MealName, parsed.Lines[0].Notes));
        Assert.Equal((new DateOnly(2025, 3, 15), MealSlot.Lunch, "Fish; chips"), (parsed.Lines[1].Date, parsed.Lines[1].Slot, parsed.Lines[1].MealName));
        Assert.Equal(1, parsed.EmptyDays);
        Assert.Contains(parsed.Warnings, w => w.Code == ImportWarningCodes.UnrecognizedLine && w.LineNumber == 5);
    }

    [Fact]
    public void Detection_tells_a_weekly_note_from_a_csv()
    {
        Assert.Equal(WeeklyNoteImportFormat.FormatId, MealPlanImportFormats.Detect(Note)?.Id);
        Assert.Equal(CsvImportFormat.FormatId, MealPlanImportFormats.Detect("2025-03-14;Lasagne\n2025-03-15;Pizza")?.Id);
        Assert.Null(MealPlanImportFormats.Detect("Just some text\nthat is not a plan"));
    }

    [Fact]
    public void The_preview_matches_existing_meals_by_key_and_only_suggests_near_matches()
    {
        var parsed = ParseNote("2026\nU2\nSø: Hotdog\nMa: Hotdogs\nTi: Hotdogs\nOn: rugbrød med fisk\nTo: Rester\nFr: Pizza");
        var guardian = new UserId(Guid.CreateVersion7());
        var rugbrød = Meal.Replay([new MealCreated(MealId.New(), guardian, guardian, "Rugbrød m. fisk", "", Icon.New("🐟"), Color.New("#000000"), DateTimeOffset.UtcNow)]);
        var occupied = ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment>.Empty
            .Add((new DateOnly(2026, 1, 9), MealSlot.Dinner), new MealPlanAssignment(rugbrød.Id, guardian, ""));

        var preview = MealPlanImportPreviewBuilder.Build(WeeklyNoteImportFormat.FormatId, parsed, [rugbrød], occupied);

        var matched = Assert.Single(preview.Groups, g => g.Key == "rugbrød m fisk");
        Assert.Equal((ImportGroupAction.Existing, rugbrød.Id), (matched.DefaultAction, matched.MatchedMealId));

        var hotdog = Assert.Single(preview.Groups, g => g.Key == "hotdog");
        Assert.Equal((ImportGroupAction.New, "hotdogs", "Hotdogs"), (hotdog.DefaultAction, hotdog.SuggestedGroupKey, hotdog.SuggestedName));
        Assert.Equal("", Assert.Single(preview.Groups, g => g.Key == "hotdogs").SuggestedGroupKey);

        Assert.Equal(ImportGroupAction.Skip, Assert.Single(preview.Groups, g => g.Key == "rester").DefaultAction);
        Assert.True(Assert.Single(preview.Lines, l => l.Key == "pizza").Occupied);
    }

    [Theory]
    [InlineData("pasta", "pizza", false)]
    [InlineData("indbagt laks m salat", "indbagt laks m salatbar", true)]
    [InlineData("butter chicken", "butterchicken", true)]
    [InlineData("tarteletter", "tartelletter", true)]
    [InlineData("lasagne", "burger", false)]
    public void Similarity_suggests_spelling_variants_only(string a, string b, bool similar)
    {
        Assert.Equal(similar, MealPlanImportPreviewBuilder.Similarity(a, b) >= 0.85);
    }

    [Fact]
    public void Days_above_week_one_belong_to_the_previous_years_last_week()
    {
        var parsed = ParseNote("Madplan 2025\nSø: Risengrød\nU1\nSø: Pizza");

        // 2024 has 52 ISO weeks; its week 52 starts Monday 23 December, so its Sunday is the 22nd.
        Assert.Equal("Risengrød", At(parsed, 2024, 12, 22).MealName);
        Assert.Equal("Pizza", At(parsed, 2024, 12, 29).MealName);
    }

    [Fact]
    public void Csv_accepts_only_slot_names_and_keeps_a_non_slot_third_column_as_notes()
    {
        const string csv = "2025-03-14;Lasagne;5\n2025-03-15;Pizza;Lunch, Dinner\n2025-03-16;Soup;extra cheese\n2025-03-17;Toast;breakfast";

        var parsed = new CsvImportFormat().Parse(csv, MealPlanImportOptions.Default) is Result<ParsedImport>.Success(var p)
            ? p
            : throw new Xunit.Sdk.XunitException("Expected the CSV to parse.");

        Assert.Equal(
            [(MealSlot.Dinner, "5"), (MealSlot.Dinner, "Lunch, Dinner"), (MealSlot.Dinner, "extra cheese"), (MealSlot.Breakfast, "")],
            parsed.Lines.Select(l => (l.Slot, l.Notes)));
    }

    [Fact]
    public void Csv_notes_are_capped_at_the_assignment_limit()
    {
        var csv = $"2025-03-14;Lasagne (cheesy);;{new string('x', 3000)}";

        var parsed = new CsvImportFormat().Parse(csv, MealPlanImportOptions.Default) is Result<ParsedImport>.Success(var p)
            ? p
            : throw new Xunit.Sdk.XunitException("Expected the CSV to parse.");

        Assert.Equal(ImportLineClassifier.MaxNotesLength, Assert.Single(parsed.Lines).Notes.Length);
    }
}
