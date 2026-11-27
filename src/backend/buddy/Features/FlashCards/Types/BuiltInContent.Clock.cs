namespace buddy.Features.FlashCards;

// Card content of the Clock sets (D13): 12 + 12 + 24 + 96 = 144 disjoint cards. The wording comes from
// DanishClock, never from hand-typed lines. The registry is BuiltInSetDescriptors and is not touched here.
public static partial class BuiltInContent
{
    // The question never carries the time words: the front of a clock card is the drawn clock face.
    public const string ClockQuestion = "Hvad er klokken?";

    private static readonly int[] ClockLevel1Minutes = [0];
    private static readonly int[] ClockLevel2Minutes = [30];
    private static readonly int[] ClockLevel3Minutes = [15, 45];
    private static readonly int[] ClockLevel4Minutes = [5, 10, 20, 25, 35, 40, 50, 55];

    public static IReadOnlyList<BuiltInCard> Clock(int level) => level switch
    {
        1 => ClockCards(ClockLevel1Minutes),
        2 => ClockCards(ClockLevel2Minutes),
        3 => ClockCards(ClockLevel3Minutes),
        4 => ClockCards(ClockLevel4Minutes),
        _ => [],
    };

    private static List<BuiltInCard> ClockCards(int[] minutes)
    {
        List<BuiltInCard> cards = [];

        for (var hour = 1; hour <= 12; hour++)
        {
            foreach (var minute in minutes)
            {
                cards.Add(new BuiltInCard(
                    $"clock:{DanishClock.Digital(hour, minute)}",
                    ClockQuestion,
                    $"{DanishClock.Words(hour, minute)} ({DanishClock.Digital(hour, minute)})",
                    new DeckClockResponse(hour, minute)));
            }
        }

        return cards;
    }
}
