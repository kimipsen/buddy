using Alba;

using buddy.Common;
using buddy.Common.Idempotency;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Common.Idempotency;

// Exercises IdempotencyKeyMiddleware through a real POST-create endpoint (CreateGroup) rather
// than unit-testing the middleware in isolation -- CreateGroup itself is already covered
// elsewhere (CreateGroupTests), so these only assert the retry-safety behavior the middleware
// adds on top.
[Collection(BuddyApiCollection.Name)]
public sealed class IdempotencyKeyTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task Retrying_a_POST_with_the_same_key_and_body_replays_the_original_response_without_creating_a_duplicate()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var key = Guid.NewGuid().ToString();

        var first = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader(IdempotencyKeyMiddleware.HeaderName, key);
            _.Post.Json(new { Name = "Book Club" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });
        var firstGroup = first.ReadAsJson<GroupResponseDto>();

        var second = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader(IdempotencyKeyMiddleware.HeaderName, key);
            _.Post.Json(new { Name = "Book Club" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });
        var secondGroup = second.ReadAsJson<GroupResponseDto>();

        Assert.Equal(firstGroup.Id, secondGroup.Id);

        var listResponse = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/groups/");
            _.StatusCodeShouldBeOk();
        });
        Assert.Single(listResponse.ReadAsJson<List<GroupSummaryDto>>(), g => g.Id == firstGroup.Id);
    }

    [Fact]
    public async Task Reusing_the_same_key_with_a_different_body_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var key = Guid.NewGuid().ToString();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader(IdempotencyKeyMiddleware.HeaderName, key);
            _.Post.Json(new { Name = "Book Club" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });

        var conflict = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.WithRequestHeader(IdempotencyKeyMiddleware.HeaderName, key);
            _.Post.Json(new { Name = "A Different Group" }).ToUrl("/groups/");
            _.StatusCodeShouldBe(409);
        });

        Assert.Equal("idempotency_key_reused", conflict.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task Without_the_header_each_POST_still_creates_its_own_resource()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var first = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "No Key Group" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });

        var second = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "No Key Group" }).ToUrl("/groups/");
            _.StatusCodeShouldBeOk();
        });

        Assert.NotEqual(first.ReadAsJson<GroupResponseDto>().Id, second.ReadAsJson<GroupResponseDto>().Id);
    }
}
