using buddy.Features.FlashCards;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class FlashCardsEventShapeTests
{
    private static readonly FlashCardSetId FixedSetId = new(Guid.Parse("00000000-0000-0000-0000-0000000000a0"));
    private static readonly FlashCardId FixedCardId = new(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly FlashCardTierId FixedTierId = new(Guid.Parse("00000000-0000-0000-0000-0000000000a2"));
    private static readonly FlashCardPracticeId FixedPracticeId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly Guid FixedGameId = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FlashCardSetCreated_for_a_math_set() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardSetCreated(FixedSetId, FixedGuardianId, "Times table of 7", FlashCardSetKind.Math, new MathRule(MathOperation.Multiplication, 7), FixedInstant),
        "FlashCards/FlashCardSetCreated_Math.json");

    [Fact]
    public void FlashCardSetCreated_for_a_text_set() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardSetCreated(FixedSetId, FixedGuardianId, "Capitals", FlashCardSetKind.Text, null, FixedInstant),
        "FlashCards/FlashCardSetCreated_Text.json");

    [Fact]
    public void FlashCardSetUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardSetUpdated(FixedSetId, "Times table of 8", new MathRule(MathOperation.Multiplication, 8), FixedGuardianId, FixedInstant),
        "FlashCards/FlashCardSetUpdated.json");

    [Fact]
    public void FlashCardAdded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardAdded(FixedSetId, new FlashCard(FixedCardId, "Capital of France?", "Paris"), FixedGuardianId, FixedInstant),
        "FlashCards/FlashCardAdded.json");

    [Fact]
    public void FlashCardUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardUpdated(FixedSetId, FixedCardId, "Capital of Denmark?", "Copenhagen", FixedGuardianId, FixedInstant),
        "FlashCards/FlashCardUpdated.json");

    [Fact]
    public void FlashCardRemoved() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardRemoved(FixedSetId, FixedCardId, FixedGuardianId, FixedInstant),
        "FlashCards/FlashCardRemoved.json");

    [Fact]
    public void FlashCardSetDeleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardSetDeleted(FixedSetId, FixedGuardianId, FixedInstant),
        "FlashCards/FlashCardSetDeleted.json");

    [Fact]
    public void FlashCardPracticeStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardPracticeStarted(FixedPracticeId, FixedChildId, FixedInstant),
        "FlashCards/FlashCardPracticeStarted.json");

    [Fact]
    public void FlashCardSetEnabled() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardSetEnabled(FixedPracticeId, FixedSetId, "Times table of 7", [new FlashCardTier(FixedTierId, 10, 1)], FixedGuardianId, FixedInstant),
        "FlashCards/FlashCardSetEnabled.json");

    [Fact]
    public void FlashCardSetDisabled() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardSetDisabled(FixedPracticeId, FixedSetId, FixedGuardianId, FixedInstant),
        "FlashCards/FlashCardSetDisabled.json");

    [Fact]
    public void FlashCardGameFinished() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new FlashCardGameFinished(
            FixedPracticeId, FixedGameId, FixedSetId, "Times table of 7", FixedTierId, new DateOnly(2025, 1, 1), 2,
            [new FlashCardResult(0, "mul:7x3", "7 x 3", "21", true, 1), new FlashCardResult(1, "mul:7x4", "7 x 4", "28", false, 2)],
            1, FixedInstant),
        "FlashCards/FlashCardGameFinished.json");

    [Fact]
    public void FlashCardGameFinished_reads_back_with_its_results()
    {
        var read = EventShapeTestSupport.ReadGoldenFile<FlashCardGameFinished>("FlashCards/FlashCardGameFinished.json");

        Assert.Equal(FixedGameId, read.GameId);
        Assert.Equal(new DateOnly(2025, 1, 1), read.FinishedOn);
        Assert.Equal(2, read.Results.Length);
        Assert.Equal(new FlashCardResult(1, "mul:7x4", "7 x 4", "28", false, 2), read.Results[1]);
    }

    [Fact]
    public void FlashCardSetEnabled_reads_back_with_its_tiers()
    {
        var read = EventShapeTestSupport.ReadGoldenFile<FlashCardSetEnabled>("FlashCards/FlashCardSetEnabled.json");

        Assert.Equal([new FlashCardTier(FixedTierId, 10, 1)], read.Tiers.ToArray());
    }
}
