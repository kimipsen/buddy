using buddy.Features.Mealplans;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

// The AI assistant's two aggregates (AiProviderCredential, MealplanAiSession) live in the
// Mealplans store, so their golden files sit under GoldenFiles/Mealplans/ with the rest.
public sealed class MealplanAiAssistantEventShapeTests
{
    private static readonly AiCredentialId FixedCredentialId = new(Guid.Parse("00000000-0000-0000-0000-000000000070"));
    private static readonly MealplanAiSessionId FixedSessionId = new(Guid.Parse("00000000-0000-0000-0000-000000000071"));
    private static readonly MealId FixedMealId = new(Guid.Parse("00000000-0000-0000-0000-000000000060"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedFrom = new(2025, 6, 2);
    private static readonly DateOnly FixedTo = new(2025, 6, 8);

    [Fact]
    public void AiCredentialsInitialized() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiCredentialsInitialized(FixedCredentialId, FixedChildId, FixedInstant),
        "Mealplans/AiCredentialsInitialized.json");

    [Fact]
    public void ProviderApiKeySet() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ProviderApiKeySet(FixedCredentialId, AiProvider.Anthropic, new StoredApiKey("CfDJ8-ciphertext", "a1b2", FixedGuardianId, FixedInstant), FixedInstant),
        "Mealplans/ProviderApiKeySet.json");

    [Fact]
    public void ProviderApiKeyRemoved() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ProviderApiKeyRemoved(FixedCredentialId, AiProvider.Gemini, FixedGuardianId, FixedInstant),
        "Mealplans/ProviderApiKeyRemoved.json");

    [Fact]
    public void ActiveProviderChanged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ActiveProviderChanged(FixedCredentialId, AiProvider.OpenAi, FixedGuardianId, FixedInstant),
        "Mealplans/ActiveProviderChanged.json");

    [Fact]
    public void ActiveProviderCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new ActiveProviderCleared(FixedCredentialId, FixedGuardianId, FixedInstant),
        "Mealplans/ActiveProviderCleared.json");

    [Fact]
    public void AiDataSharingAcknowledged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiDataSharingAcknowledged(FixedCredentialId, FixedGuardianId, FixedInstant),
        "Mealplans/AiDataSharingAcknowledged.json");

    [Fact]
    public void AiSessionStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiSessionStarted(FixedSessionId, FixedChildId, FixedFrom, FixedTo, [MealSlot.Lunch, MealSlot.Dinner], [FixedMealId], "No fish on Fridays", FixedGuardianId, FixedInstant),
        "Mealplans/AiSessionStarted.json");

    [Fact]
    public void AiSessionStarted_Filtered() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiSessionStarted(FixedSessionId, FixedChildId, FixedFrom, FixedTo, [MealSlot.Lunch, MealSlot.Dinner], [FixedMealId], "No fish on Fridays", FixedGuardianId, FixedInstant,
            RatedOnly: true, ServedWithin: AiServedWindow.Last60Days),
        "Mealplans/AiSessionStarted_Filtered.json");

    [Fact]
    public void AiUserMessageSent() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiUserMessageSent(FixedSessionId, "Plan dinners for the week", FixedGuardianId, FixedInstant),
        "Mealplans/AiUserMessageSent.json");

    [Fact]
    public void AiToolInvocationRecorded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiToolInvocationRecorded(FixedSessionId, "toolu_01", "set_draft_assignment", """{"date":"2025-06-02","slot":"Dinner"}""", """{"ok":true}""", false, FixedInstant),
        "Mealplans/AiToolInvocationRecorded.json");

    [Fact]
    public void AiDraftAssignmentSet() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiDraftAssignmentSet(FixedSessionId, FixedFrom, MealSlot.Dinner, FixedMealId, FixedInstant),
        "Mealplans/AiDraftAssignmentSet.json");

    [Fact]
    public void AiDraftAssignmentCleared() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiDraftAssignmentCleared(FixedSessionId, FixedFrom, MealSlot.Dinner, FixedInstant),
        "Mealplans/AiDraftAssignmentCleared.json");

    [Fact]
    public void AiAssistantMessageRecorded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiAssistantMessageRecorded(FixedSessionId, "Here is a draft for the week.", FixedInstant),
        "Mealplans/AiAssistantMessageRecorded.json");

    [Fact]
    public void AiSessionApplied() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiSessionApplied(FixedSessionId, FixedGuardianId, FixedInstant),
        "Mealplans/AiSessionApplied.json");

    [Fact]
    public void AiSessionDiscarded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiSessionDiscarded(FixedSessionId, FixedGuardianId, FixedInstant),
        "Mealplans/AiSessionDiscarded.json");

    [Fact]
    public void AiSessionExpired() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new AiSessionExpired(FixedSessionId, FixedInstant),
        "Mealplans/AiSessionExpired.json");
}
