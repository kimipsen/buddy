using System.Collections.Frozen;

namespace buddy.Features.Feelings;

// Four zones, each shown with a face, a colour and a word; there's no separate face scale in the
// data (docs/backend/analysis/feelings-check-in.md, Question 2). Persisted by name, so a member is
// never renamed, only added.
public enum FeelingZone
{
    Blue,
    Green,
    Yellow,
    Red
}

// A fixed, translated catalog (Question 4): feeling words that belong to one zone, and situations
// that fit any zone. Persisted by name -- never rename one.
public enum FeelingTag
{
    // Blue
    Tired,
    Sad,
    Bored,
    Unwell,
    Lonely,

    // Green
    Happy,
    Calm,
    Focused,
    Proud,
    Okay,

    // Yellow
    Worried,
    Frustrated,
    Silly,
    Excited,
    Restless,

    // Red
    Angry,
    Scared,
    Overwhelmed,

    // Situations (any zone)
    School,
    Home,
    Friends,
    Homework,
    Screens,
    Food,
    Bedtime,
    Body
}

// What the child's four buttons show (Question 9). FacesOnly hides the zone words for a child who
// can't read them yet; the stored zones are the same either way.
public enum FeelingsDisplayMode
{
    FacesAndWords,
    FacesOnly
}

public static class FeelingTags
{
    private static readonly FrozenDictionary<FeelingTag, FeelingZone> FeelingWordZones = new Dictionary<FeelingTag, FeelingZone>
    {
        [FeelingTag.Tired] = FeelingZone.Blue,
        [FeelingTag.Sad] = FeelingZone.Blue,
        [FeelingTag.Bored] = FeelingZone.Blue,
        [FeelingTag.Unwell] = FeelingZone.Blue,
        [FeelingTag.Lonely] = FeelingZone.Blue,
        [FeelingTag.Happy] = FeelingZone.Green,
        [FeelingTag.Calm] = FeelingZone.Green,
        [FeelingTag.Focused] = FeelingZone.Green,
        [FeelingTag.Proud] = FeelingZone.Green,
        [FeelingTag.Okay] = FeelingZone.Green,
        [FeelingTag.Worried] = FeelingZone.Yellow,
        [FeelingTag.Frustrated] = FeelingZone.Yellow,
        [FeelingTag.Silly] = FeelingZone.Yellow,
        [FeelingTag.Excited] = FeelingZone.Yellow,
        [FeelingTag.Restless] = FeelingZone.Yellow,
        [FeelingTag.Angry] = FeelingZone.Red,
        [FeelingTag.Scared] = FeelingZone.Red,
        [FeelingTag.Overwhelmed] = FeelingZone.Red,
    }.ToFrozenDictionary();

    // A feeling word fits only its own zone ("a child who taps yellow sees the yellow words"); a
    // situation tag fits every zone.
    public static bool FitsZone(FeelingTag tag, FeelingZone zone) =>
        !FeelingWordZones.TryGetValue(tag, out var wordZone) || wordZone == zone;
}
