namespace buddy.Features.FlashCards;

// The registry of the 16 built-in sets, in catalogue order. Card content lives in the
// BuiltInContent.*.cs files and is the only thing the content slices touch.
public static class BuiltInSetDescriptors
{
    private static readonly FlashCardSetId Clock1 = new(new Guid("0aed9cb7-132c-5087-af12-0bbc13ab94db"));
    private static readonly FlashCardSetId Clock2 = new(new Guid("899219e1-50fd-551d-8410-a240ddaa4922"));
    private static readonly FlashCardSetId Clock3 = new(new Guid("2a8a969e-8044-53eb-b43b-54fbf18c30ee"));
    private static readonly FlashCardSetId Clock4 = new(new Guid("fbfb5186-3421-542d-89dd-675d90daf6e9"));
    private static readonly FlashCardSetId Sight1 = new(new Guid("56141e33-d8de-5d82-8993-d7fe778af4fc"));
    private static readonly FlashCardSetId Sight2 = new(new Guid("4dee9bcc-6bd2-5047-832f-8cc6cb380292"));
    private static readonly FlashCardSetId Sight3 = new(new Guid("93dc34ee-b4d7-52d6-b5aa-2bd41756d924"));
    private static readonly FlashCardSetId Sight4 = new(new Guid("f1bde22b-bb23-5589-85b6-5850287b995a"));
    private static readonly FlashCardSetId Letters1 = new(new Guid("31e034bf-3d77-5cb0-8d1d-394bf0e38286"));
    private static readonly FlashCardSetId Letters2 = new(new Guid("3db2d210-daa5-5ccf-b71b-c32a70150d9b"));
    private static readonly FlashCardSetId English1 = new(new Guid("2dfede7a-86db-5af1-a2c6-38adcf2d3174"));
    private static readonly FlashCardSetId English2 = new(new Guid("4fca12b4-ab47-5de6-8b3c-ae9fed77650f"));
    private static readonly FlashCardSetId English3 = new(new Guid("a0f02591-f412-5cf1-a017-b64e8358d6ab"));
    private static readonly FlashCardSetId Countries1 = new(new Guid("a6e6bc6c-0a87-5c1b-b18b-40f2d8669055"));
    private static readonly FlashCardSetId Countries2 = new(new Guid("f6813f89-979f-5c91-a2ab-a7ab2b4008ad"));
    private static readonly FlashCardSetId Countries3 = new(new Guid("2f55a512-d856-5351-8a61-15401378874f"));

    public static IReadOnlyList<BuiltInSetDescriptor> All { get; } =
    [
        new(Clock1, "clock:1", "Klokken: hele timer", BuiltInContent.Clock(1)),
        new(Clock2, "clock:2", "Klokken: halve timer", BuiltInContent.Clock(2)),
        new(Clock3, "clock:3", "Klokken: kvarter", BuiltInContent.Clock(3)),
        new(Clock4, "clock:4", "Klokken: hver 5. minut", BuiltInContent.Clock(4)),
        new(Sight1, "sight:1", "Sight words: 0. klasse", BuiltInContent.SightWords(1)),
        new(Sight2, "sight:2", "Sight words: 1. klasse", BuiltInContent.SightWords(2)),
        new(Sight3, "sight:3", "Sight words: 2. klasse", BuiltInContent.SightWords(3)),
        new(Sight4, "sight:4", "Sight words: svære ord", BuiltInContent.SightWords(4)),
        new(Letters1, "letters:1", "Bogstaver: store", BuiltInContent.Letters(1)),
        new(Letters2, "letters:2", "Bogstaver: små", BuiltInContent.Letters(2)),
        new(English1, "english:1", "Engelsk: niveau 1", BuiltInContent.English(1)),
        new(English2, "english:2", "Engelsk: niveau 2", BuiltInContent.English(2)),
        new(English3, "english:3", "Engelsk: niveau 3", BuiltInContent.English(3)),
        new(Countries1, "countries:1", "Lande: Norden", BuiltInContent.Countries(1)),
        new(Countries2, "countries:2", "Lande: Europa", BuiltInContent.Countries(2)),
        new(Countries3, "countries:3", "Lande: Verden", BuiltInContent.Countries(3)),
    ];
}
