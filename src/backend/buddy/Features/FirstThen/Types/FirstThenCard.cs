using buddy.Features.Calendars;

namespace buddy.Features.FirstThen;

// A short title plus one emoji, the same shape as a reward. A copy, never a reference to a task or
// a reward (first-then-board.md, Question 3).
public sealed record FirstThenCard(string Title, Icon Icon)
{
    // What a board in state None holds instead of a nullable card (eliminate-nulls.md).
    public static readonly FirstThenCard Empty = new("", new Icon(""));

    public bool HasSameTitleAs(FirstThenCard other) =>
        string.Equals(Title, other.Title, StringComparison.OrdinalIgnoreCase);
}
