using buddy.Features.Mealplans;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// Matches AiProviderSettings / AiProviderSettingsEntry (Features/Mealplans/AiAssistant/Types).
internal sealed record AiProviderSettingsEntryDto(AiProvider Provider, string Last4, DateTimeOffset AddedAt);

internal sealed record AiProviderSettingsDto(IReadOnlyList<AiProviderSettingsEntryDto> Providers, AiProvider? ActiveProvider);
