using buddy.Features.Mealplans;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// Matches AiProviderSettings / AiProviderSettingsEntry (Features/Mealplans/AiAssistant/Types).
internal sealed record AiProviderSettingsEntryDto(AiProvider Provider, string Last4, DateTimeOffset AddedAt);

internal sealed record AiProviderSettingsDto(IReadOnlyList<AiProviderSettingsEntryDto> Providers, AiProvider? ActiveProvider, DateTimeOffset? DataSharingAcknowledgedAt = null);

// TestProviderConnectionResult read flat: kind 0 succeeded, kind 1 failed with a message.
internal sealed record TestProviderConnectionResultDto(int Kind, string? Message = null);

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
