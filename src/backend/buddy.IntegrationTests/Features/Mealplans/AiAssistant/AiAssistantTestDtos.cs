using buddy.Features.Mealplans;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// Matches AiProviderSettings / AiProviderSettingsEntry (Features/Mealplans/AiAssistant/Types).
internal sealed record AiProviderSettingsEntryDto(AiProvider Provider, string Last4, DateTimeOffset AddedAt);

internal sealed record AiProviderSettingsDto(IReadOnlyList<AiProviderSettingsEntryDto> Providers, AiProvider? ActiveProvider);

internal sealed record TestProviderConnectionResultDto(bool IsSuccessful, string? ErrorMessage);

// Matches AiSessionView / AiSessionTranscriptEntry / AiSessionDraftEntry (Features/Mealplans/AiAssistant/Types).
internal sealed record AiSessionTranscriptEntryDto(AiChatMessageRole Role, string Text, DateTimeOffset OccurredAt);

internal sealed record AiSessionDraftEntryDto(DateOnly Date, MealSlot Slot, Guid MealId, string MealName);

internal sealed record AiSessionViewDto(
    Guid Id,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<MealSlot> RequestedSlots,
    AiSessionStatus Status,
    IReadOnlyList<AiSessionTranscriptEntryDto> Transcript,
    IReadOnlyList<AiSessionDraftEntryDto> Draft);
