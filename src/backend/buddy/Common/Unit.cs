namespace buddy.Common;

// Stands in for "no value" as Result<Unit>, for commands that only report success or failure.
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
