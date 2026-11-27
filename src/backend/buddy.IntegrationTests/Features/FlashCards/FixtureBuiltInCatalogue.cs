using buddy.Features.FlashCards;

namespace buddy.IntegrationTests.Features.FlashCards;

// The built-in catalogue the shared test host runs with (BuddyApiFixture): a handful of synthetic sets,
// so a test never depends on the shipped content. Real-content assertions are plain unit tests on
// BuiltInCatalogue.Default (BuiltInCatalogueTests).
internal static class FixtureBuiltInCatalogue
{
    public const string RetiredKey = "retired:old-card";

    // Three clock cards: smaller than any tier's CardCount, so a deck repeats.
    public static readonly FlashCardSetId ClockId = new(new Guid("11111111-0000-5000-8000-000000000001"));

    // Fifteen flag cards: a pool larger than the five-card tier.
    public static readonly FlashCardSetId FlagsId = new(new Guid("11111111-0000-5000-8000-000000000002"));

    // Sight words: the answer is the word itself.
    public static readonly FlashCardSetId WordsId = new(new Guid("11111111-0000-5000-8000-000000000003"));

    // No cards yet, like every real set before its content slice: cannot be enabled, ExampleCard is null.
    public static readonly FlashCardSetId EmptyId = new(new Guid("11111111-0000-5000-8000-000000000004"));

    public static readonly string[] FlagCodes = ["DK", "SE", "NO", "FI", "IS", "DE", "FR", "ES", "IT", "NL", "BE", "PL", "AT", "CH", "IE"];

    public static readonly IReadOnlyList<BuiltInSetDescriptor> Descriptors =
    [
        new(ClockId, "fixture:clock", "Fixture: urskive",
        [
            new("clock:3:30", "Hvad er klokken?", "halv fire (3:30)", new DeckClockResponse(3, 30)),
            new("clock:6:00", "Hvad er klokken?", "klokken seks (6:00)", new DeckClockResponse(6, 0)),
            new("clock:9:15", "Hvad er klokken?", "kvart over ni (9:15)", new DeckClockResponse(9, 15))
        ]),
        new(FlagsId, "fixture:flags", "Fixture: flag", [.. FlagCodes.Select(code => new BuiltInCard($"flag:{code}", $"flag {code}", $"land {code}"))]),
        new(WordsId, "fixture:words", "Fixture: ord",
        [
            new("sight:og", "og", "og"),
            new("sight:det", "det", "det"),
            new("sight:en", "en", "en"),
            new("sight:jeg", "jeg", "jeg"),
            new("sight:er", "er", "er")
        ]),
        new(EmptyId, "fixture:empty", "Fixture: tom", [])
    ];

    public static readonly BuiltInCatalogue Instance = new(Descriptors, new HashSet<string> { RetiredKey });
}
