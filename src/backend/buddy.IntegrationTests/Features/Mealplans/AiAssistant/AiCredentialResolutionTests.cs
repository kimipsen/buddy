using Alba;

using buddy.Features.Guardians;
using buddy.Features.Mealplans;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.AiAssistant;

// The family AI credential is indexed under the one child it was first set up through
// (AiCredentialIndexDocument). These pin the resolution rule in
// MealFamilyResolution.ResolveFamilyAiCredentialIdAsync -- see docs/backend/mealplans/flow.md,
// "AI credential resolution".
[Collection(BuddyApiCollection.Name)]
public sealed class AiCredentialResolutionTests(BuddyApiFixture fixture)
{
    [Fact]
    public async Task Unlinking_the_child_that_holds_the_credential_keeps_the_key_available_through_another_linked_child()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var holder = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Holder");
        var sibling = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken, "Sibling");
        await SetKeyAsync(guardianToken, holder.Id, AiProvider.Anthropic, "sk-ant-key-1111");

        await RevokeLinkAsync(guardianToken, holder.Id);

        var listed = await ListProvidersAsync(guardianToken, sibling.Id);
        Assert.Equal(AiProvider.Anthropic, listed.ActiveProvider);
        Assert.Equal("1111", Assert.Single(listed.Providers).Last4);

        // A write through the remaining child lands on the same credential rather than
        // provisioning a second one next to it.
        var updated = await SetKeyAsync(guardianToken, sibling.Id, AiProvider.OpenAi, "sk-oai-key-2222");
        Assert.Equal(new[] { "1111", "2222" }, updated.Providers.Select(p => p.Last4).Order());
        Assert.Equal(AiProvider.Anthropic, updated.ActiveProvider);
    }

    [Fact]
    public async Task A_guardian_who_leaves_a_family_does_not_inherit_a_key_they_never_touched()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (coGuardian, coGuardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var sharedChild = await GuardianTestHelpers.CreateChildAsync(fixture, ownerToken, "Shared");
        await SetKeyAsync(ownerToken, sharedChild.Id, AiProvider.Anthropic, "sk-ant-key-3333");
        await LinkGuardianAsync(ownerToken, sharedChild.Id, coGuardian, coGuardianToken);
        var ownChild = await GuardianTestHelpers.CreateChildAsync(fixture, coGuardianToken, "Own");

        // While linked, the co-guardian's own child is in the same family and sees the key.
        Assert.Equal("3333", Assert.Single((await ListProvidersAsync(coGuardianToken, ownChild.Id)).Providers).Last4);

        await RevokeLinkAsync(coGuardianToken, sharedChild.Id);

        var listed = await ListProvidersAsync(coGuardianToken, ownChild.Id);
        Assert.Empty(listed.Providers);
        Assert.Null(listed.ActiveProvider);

        // The family that stayed keeps its key.
        Assert.Equal("3333", Assert.Single((await ListProvidersAsync(ownerToken, sharedChild.Id)).Providers).Last4);
    }

    [Fact]
    public async Task With_two_credentials_across_merged_families_the_most_recently_activated_one_is_chosen_consistently()
    {
        var (firstGuardian, firstToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, secondToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var firstChild = await GuardianTestHelpers.CreateChildAsync(fixture, firstToken, "First");
        var secondChild = await GuardianTestHelpers.CreateChildAsync(fixture, secondToken, "Second");

        // The first family's credential is created first (older id) ...
        await SetKeyAsync(firstToken, firstChild.Id, AiProvider.Anthropic, "sk-ant-key-4444");
        // ... the second family's is created and activated after it ...
        await SetKeyAsync(secondToken, secondChild.Id, AiProvider.Gemini, "gm-key-5555");
        // ... and then the first family activates a different provider, making the older
        // credential the most recently activated one.
        await SetKeyAsync(firstToken, firstChild.Id, AiProvider.OpenAi, "sk-oai-key-6666");
        await SetActiveProviderAsync(firstToken, firstChild.Id, AiProvider.OpenAi);

        // Merge: the first guardian becomes a guardian of the second child too, so both children
        // (and both credentials) are now in one family.
        await LinkGuardianAsync(secondToken, secondChild.Id, firstGuardian, firstToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            foreach (var (token, childId) in new[] { (firstToken, firstChild.Id), (firstToken, secondChild.Id), (secondToken, secondChild.Id) })
            {
                var listed = await ListProvidersAsync(token, childId);
                Assert.Equal(AiProvider.OpenAi, listed.ActiveProvider);
                Assert.Equal(new[] { "4444", "6666" }, listed.Providers.Select(p => p.Last4).Order());
            }
        }

        // Re-activating on the resolved credential keeps it the winner (the other one can't be
        // reached through the API while it loses).
        var switched = await SetActiveProviderAsync(secondToken, secondChild.Id, AiProvider.Anthropic);
        Assert.Equal(AiProvider.Anthropic, switched.ActiveProvider);
        var fromFirstFamily = await ListProvidersAsync(firstToken, firstChild.Id);
        Assert.Equal(AiProvider.Anthropic, fromFirstFamily.ActiveProvider);
        Assert.Equal(new[] { "4444", "6666" }, fromFirstFamily.Providers.Select(p => p.Last4).Order());
    }

    private async Task<AiProviderSettingsDto> SetKeyAsync(string token, Guid childId, AiProvider provider, string apiKey)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new { ApiKey = apiKey }).ToUrl($"/mealplans/children/{childId}/ai/providers/{provider}/key");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<AiProviderSettingsDto>();
    }

    private async Task<AiProviderSettingsDto> SetActiveProviderAsync(string token, Guid childId, AiProvider provider)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Url($"/mealplans/children/{childId}/ai/active-provider/{provider}");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<AiProviderSettingsDto>();
    }

    private async Task<AiProviderSettingsDto> ListProvidersAsync(string token, Guid childId)
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url($"/mealplans/children/{childId}/ai/providers");
            _.StatusCodeShouldBeOk();
        });

        return response.ReadAsJson<AiProviderSettingsDto>();
    }

    private async Task RevokeLinkAsync(string token, Guid childId) =>
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/users/me/children/{childId}/guardian-link");
            _.StatusCodeShouldBe(204);
        });

    private async Task LinkGuardianAsync(string inviterToken, Guid childId, TestUser invitee, string inviteeToken)
    {
        await GuardianTestHelpers.InviteGuardianAsync(fixture, inviterToken, childId, invitee.Email, GuardianKind.Parent);
        var token = await GuardianTestHelpers.ReadGuardianInviteTokenAsync(fixture, invitee.Email);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {inviteeToken}");
            _.Post.Url($"/guardian-invites/{token}/accept");
            _.StatusCodeShouldBe(204);
        });
    }
}
