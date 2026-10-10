using Alba;

using buddy.Features.Groups;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

namespace buddy.IntegrationTests.Features.HouseRules;

// Response shapes matching RuleBookResponse / ChildRulesResponse (Features/HouseRules).
internal sealed record RuleAcknowledgementDto(Guid ChildId, int? AcknowledgedRevision, bool IsUpToDate);

internal sealed record RuleDto(
    Guid Id,
    string Title,
    string Body,
    int Revision,
    int AcknowledgementRevision,
    DateTimeOffset LastEditedAt,
    List<RuleAcknowledgementDto> Acknowledgements);

internal sealed record RuleBookDto(string ScopeKind, Guid ScopeId, string Access, List<Guid> Children, List<RuleDto> Rules);

internal sealed record ChildRuleDto(Guid Id, string Title, string Body, int Revision, int? AcknowledgedRevision, bool IsUpToDate, DateTimeOffset LastEditedAt);

internal sealed record ChildRuleSectionDto(string ScopeKind, Guid ScopeId, string Label, List<ChildRuleDto> Rules);

internal sealed record ChildRulesDto(Guid ChildId, ChildRuleSectionDto Personal, List<ChildRuleSectionDto> Households, int PendingAcknowledgements);

// One household group with every tier in it: the owner (a guardian of Child, Manage), the child as a
// Member (Acknowledge), and another adult as a plain Member (View).
internal sealed record Household(
    Guid GroupId,
    string OwnerToken,
    Guid OwnerId,
    ChildResponseDto Child,
    string ChildToken,
    string AdultToken,
    Guid AdultId)
{
    public string Rules => HouseRulesTestHelpers.GroupRules(GroupId);
}

internal static class HouseRulesTestHelpers
{
    public static string ChildRules(Guid childId) => $"/house-rules/children/{childId}/rules";

    public static string GroupRules(Guid groupId) => $"/house-rules/groups/{groupId}/rules";

    public static async Task<Household> CreateHouseholdAsync(BuddyApiFixture fixture, string name = "Home")
    {
        var (_, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, ownerToken, "Emil");
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);
        var (adult, adultToken, adultId) = await fixture.CreateAuthenticatedUserAsync();

        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, name);
        await AddChildToGroupAsync(fixture, ownerToken, groupId, child.Id);
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, adultToken, adult.Email, GroupRole.Member);

        return new Household(groupId, ownerToken, ownerId, child, childToken, adultToken, adultId);
    }

    public static Task AddChildToGroupAsync(BuddyApiFixture fixture, string guardianToken, Guid groupId, Guid childId) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {guardianToken}");
            _.Put.Url($"/groups/{groupId}/children/{childId}");
            _.StatusCodeShouldBe(204);
        });

    public static async Task<RuleBookDto?> AddRuleAsync(
        BuddyApiFixture fixture, string token, string rulesPath, string title, string body = "", int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Title = title, Body = body }).ToUrl(rulesPath);
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<RuleBookDto>() : null;
    }

    // Adds one rule and returns it (the last one in the book).
    public static async Task<RuleDto> AddOneRuleAsync(BuddyApiFixture fixture, string token, string rulesPath, string title = "Screen time", string body = "- 45 min")
    {
        var book = await AddRuleAsync(fixture, token, rulesPath, title, body);

        return book!.Rules[^1];
    }

    public static async Task<RuleBookDto?> EditRuleAsync(
        BuddyApiFixture fixture,
        string token,
        string rulesPath,
        Guid ruleId,
        string title,
        string body = "",
        bool requireReacknowledgement = true,
        int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Title = title, Body = body, RequireReacknowledgement = requireReacknowledgement }).ToUrl($"{rulesPath}/{ruleId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<RuleBookDto>() : null;
    }

    public static Task RemoveRuleAsync(BuddyApiFixture fixture, string token, string rulesPath, Guid ruleId, int expectedStatus = 204) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"{rulesPath}/{ruleId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<RuleBookDto?> ReorderRulesAsync(
        BuddyApiFixture fixture, string token, string rulesPath, IEnumerable<Guid> newOrder, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { NewOrder = newOrder.ToArray() }).ToUrl($"{rulesPath}/order");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<RuleBookDto>() : null;
    }

    public static async Task<RuleBookDto?> ListRulesAsync(BuddyApiFixture fixture, string token, string rulesPath, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url(rulesPath);
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<RuleBookDto>() : null;
    }

    public static Task<IScenarioResult> AcknowledgeAsync(
        BuddyApiFixture fixture, string token, string rulesPath, Guid ruleId, int revision, Guid? childId = null, int expectedStatus = 204) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { Revision = revision, ChildId = childId }).ToUrl($"{rulesPath}/{ruleId}/acknowledgement");
            _.StatusCodeShouldBe(expectedStatus);
        });

    public static async Task<ChildRulesDto?> GetChildRulesAsync(BuddyApiFixture fixture, string token, Guid childId, int expectedStatus = 200)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/house-rules/children/{childId}");
            _.StatusCodeShouldBe(expectedStatus);
        });

        return expectedStatus == 200 ? response.ReadAsJson<ChildRulesDto>() : null;
    }
}
