namespace buddy.Features.FirstThen;

// None: no board. Waiting: the First is still to do. Unlocked: the First is done, time for the
// Then. See first-then-board.md, Question 4.
public enum FirstThenState
{
    None,
    Waiting,
    Unlocked
}
