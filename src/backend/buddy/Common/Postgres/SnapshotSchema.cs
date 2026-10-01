namespace buddy.Common.Postgres;

// Every feature's inline Marten snapshot projection lives in this shared Postgres schema, never
// in the feature's own event schema -- snapshots are derived, rebuildable state, not a second
// source of truth. See docs/backend/analysis/event-stream-snapshots.md.
public static class SnapshotSchema
{
    public const string Name = "snapshots";
}
