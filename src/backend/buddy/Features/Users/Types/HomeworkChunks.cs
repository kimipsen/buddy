namespace buddy.Features.Users;

// How a child's homework is split (docs/frontend/analysis/homework-splitter.md, Question 2): focus
// chunks of FocusMinutes with BreakMinutes between them. Guardian-managed per child
// (UpdateChildHomeworkChunks); the splitting and the session itself stay in the child's browser.
public sealed record HomeworkChunks(int FocusMinutes, int BreakMinutes)
{
    public const int MinFocusMinutes = 5;
    public const int MaxFocusMinutes = 45;
    public const int MinBreakMinutes = 1;
    public const int MaxBreakMinutes = 15;

    public static readonly HomeworkChunks Default = new(15, 5);
}
