namespace buddy.Features.Users;

public static class SupportedLanguages
{
    public static readonly Language Default = Language.New("en");

    public static readonly IReadOnlyList<Language> All = [Default, Language.New("da")];

    public static bool IsValid(Language language) => All.Any(l => l.Value == language.Value);

    // Accept-Language sends locale tags like "da-DK" or "en-US;q=0.8", ranked by descending
    // quality. Only the primary subtag is matched against the supported set -- this backend
    // doesn't distinguish regional variants of a language.
    public static Language ResolveFromAcceptLanguageHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return Default;
        }

        var ranked = header
            .Split(',')
            .Select(ParseRange)
            .OrderByDescending(range => range.Quality);

        foreach (var (primarySubtag, _) in ranked)
        {
            var match = All.FirstOrDefault(l => l.Value == primarySubtag);

            if (match is not null)
            {
                return match;
            }
        }

        return Default;
    }

    private static (string PrimarySubtag, double Quality) ParseRange(string range)
    {
        var parts = range.Trim().Split(';', 2);
        var tag = parts[0].Trim();
        var primarySubtag = tag.Split('-', 2)[0].ToLowerInvariant();

        var quality = 1.0;

        if (parts.Length == 2)
        {
            var qualityPart = parts[1].Trim();

            if (qualityPart.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(qualityPart[2..], System.Globalization.CultureInfo.InvariantCulture, out var parsedQuality))
            {
                quality = parsedQuality;
            }
        }

        return (primarySubtag, quality);
    }
}
