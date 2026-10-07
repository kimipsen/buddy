namespace buddy.Features.Users;

// One erased user: the UserId (a pseudonym, nothing else) and when. Restoring a backup brings erased
// people back; the restore procedure re-imports this ledger and UserErasureService erases them again.
// See docs/backend/analysis/gdpr-data-protection.md, Backups.
public sealed record ErasureLedgerEntry(Guid Id, DateTimeOffset ErasedAt);
