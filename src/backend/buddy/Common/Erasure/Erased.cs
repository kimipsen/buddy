namespace buddy.Common.Erasure;

// What a masked personal value becomes (see docs/backend/analysis/gdpr-data-protection.md). One
// fixed placeholder, so masked data is recognisable in the database and in the app.
public static class Erased
{
    public const string Text = "[erased]";
}
