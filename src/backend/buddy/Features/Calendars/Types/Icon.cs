namespace buddy.Features.Calendars;

// An emoji ("🪥") or a pictogram from the frontend's bundled set ("pictogram:brush-teeth", the
// prefix plus the SVG's file name without .svg). Both are plain strings on the wire and in events
// (docs/frontend/analysis/pictograms.md, Question 2).
public sealed record Icon(string Value)
{
    public const string PictogramPrefix = "pictogram:";

    // One emoji, possibly with skin-tone and ZWJ modifiers.
    public const int MaxEmojiLength = 16;

    public const int MaxLength = 64;

    public static Icon New(string value) => new(value);

    public bool IsPictogram => Value.StartsWith(PictogramPrefix, StringComparison.Ordinal);
}
