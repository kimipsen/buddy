namespace buddy.Features.Users;

// How a child wants their visual timers to look and sound
// (docs/frontend/analysis/child-timer-preferences.md, Question 1). Self-managed by the child
// (UpdateTimerPreferences); the defaults are the timer as it was before the child could choose.
// Shape and Size only change the big timers (focus view, homework, First-Then, my timer); Color
// is a palette name, not a hex code, so the frontend can retune the palette without new events.
public sealed record TimerPreferences(
    TimerShape Shape,
    TimerSize Size,
    TimerColor Color,
    bool ShowMinutes,
    bool Chime,
    ChimeSound ChimeSound,
    bool ChimeOnHome)
{
    public static readonly TimerPreferences Default =
        new(TimerShape.Disc, TimerSize.Large, TimerColor.Routine, ShowMinutes: true, Chime: true, ChimeSound.Bell, ChimeOnHome: false);
}

public enum TimerShape
{
    Disc,
    Bar,
}

public enum TimerSize
{
    Small,
    Medium,
    Large,
}

// Routine is the run's own colour, as the guardian picked it in the task library.
public enum TimerColor
{
    Routine,
    Green,
    Blue,
    Purple,
    Pink,
    Orange,
    Yellow,
}

public enum ChimeSound
{
    Bell,
    Xylophone,
    Gong,
}
