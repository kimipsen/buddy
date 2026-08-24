namespace buddy.Features.Users;

// An ISO 639-1 code (e.g. "en", "da"). Membership in SupportedLanguages is checked in handlers,
// not here, to keep this type a plain data holder like the other value types.
public sealed record Language(string Value)
{
    public static Language New(string value) => new(value);
}
