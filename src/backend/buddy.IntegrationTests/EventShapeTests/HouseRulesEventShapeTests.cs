using buddy.Features.HouseRules;
using buddy.Features.Users;

using Xunit;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class HouseRulesEventShapeTests
{
    private static readonly UserId FixedChildId = new(Guid.Parse("00000000-0000-0000-0000-000000000003"));
    private static readonly UserId FixedGuardianId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly Guid FixedGroupId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly RuleBookId FixedBookId = RuleBookId.ForChild(FixedChildId);
    private static readonly RuleId FixedRuleId = new(Guid.Parse("00000000-0000-0000-0000-0000000000b1"));
    private static readonly RuleId OtherRuleId = new(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly RuleContent Before = new("Screen time", "- 45 min after homework");
    private static readonly RuleContent After = new("Screen time", "| Day | Time |\n|---|---|\n| Mon | 45 min |");

    [Fact]
    public void RuleBookStarted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RuleBookStarted(FixedBookId, RuleBookScopeKind.Child, FixedChildId.Value, FixedGuardianId, FixedInstant),
        "HouseRules/RuleBookStarted.json");

    [Fact]
    public void RuleBookStarted_for_a_group() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RuleBookStarted(new RuleBookId(FixedGroupId), RuleBookScopeKind.Group, FixedGroupId, FixedGuardianId, FixedInstant),
        "HouseRules/RuleBookStarted_Group.json");

    [Fact]
    public void RuleAdded() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RuleAdded(FixedBookId, FixedRuleId, "Shoes off at the door", "", FixedGuardianId, FixedInstant),
        "HouseRules/RuleAdded.json");

    [Fact]
    public void RuleEdited() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RuleEdited(FixedBookId, FixedRuleId, Before, After, 2, true, FixedGuardianId, FixedInstant),
        "HouseRules/RuleEdited.json");

    [Fact]
    public void RuleRemoved() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RuleRemoved(FixedBookId, FixedRuleId, Before, FixedGuardianId, FixedInstant),
        "HouseRules/RuleRemoved.json");

    [Fact]
    public void RulesReordered() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RulesReordered(FixedBookId, [FixedRuleId, OtherRuleId], [OtherRuleId, FixedRuleId], FixedGuardianId, FixedInstant),
        "HouseRules/RulesReordered.json");

    [Fact]
    public void RuleAcknowledged() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RuleAcknowledged(FixedBookId, FixedRuleId, FixedChildId, 2, FixedChildId, FixedInstant),
        "HouseRules/RuleAcknowledged.json");

    [Fact]
    public void RuleAcknowledged_by_a_guardian() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new RuleAcknowledged(FixedBookId, FixedRuleId, FixedChildId, 2, FixedGuardianId, FixedInstant),
        "HouseRules/RuleAcknowledged_ByGuardian.json");
}
