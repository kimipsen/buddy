namespace buddy.Features.Mealplans;

// The masked, read-facing shape of an AiProviderCredential -- returned by every AiAssistant
// provider-management endpoint so the full key is never round-tripped back to a client.
public sealed record AiProviderSettings(
    IReadOnlyCollection<AiProviderSettingsEntry> Providers,
    AiProvider? ActiveProvider,
    DateTimeOffset? DataSharingAcknowledgedAt)
{
    public static readonly AiProviderSettings Empty = new([], null, null);

    public static AiProviderSettings FromCredential(AiProviderCredential credential) =>
        new(
            [.. credential.Providers.Select(pair => new AiProviderSettingsEntry(pair.Key, pair.Value.Last4, pair.Value.AddedAt))],
            credential.ActiveProvider,
            credential.DataSharingAcknowledgedAt);
}

public sealed record AiProviderSettingsEntry(AiProvider Provider, string Last4, DateTimeOffset AddedAt);
