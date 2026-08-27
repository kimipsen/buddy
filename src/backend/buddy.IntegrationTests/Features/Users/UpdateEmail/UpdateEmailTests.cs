using Alba;

using buddy.Common;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.UpdateEmail;

[Collection(BuddyApiCollection.Name)]
public sealed class UpdateEmailTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("UpdateCurrentEmail")]
    public async Task Changing_the_email_drops_back_to_unverified_and_sends_a_new_verification_email()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var newEmail = $"changed-{Guid.NewGuid():N}@buddy.test";

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = newEmail }).ToUrl("/users/me/email");
            _.StatusCodeShouldBeOk();
        });

        var body = response.ReadAsJson<EmailResponseEnvelope>();

        Assert.Equal(newEmail, body.Email.Value);
        Assert.False(body.Email.IsVerified);

        var messages = await fixture.GetMailpitMessagesToAsync(newEmail);
        Assert.NotEmpty(messages);
    }

    [Fact]
    public async Task Rejects_an_empty_email()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = "" }).ToUrl("/users/me/email");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Value", error.Details.Keys);
    }

    [Fact]
    public async Task Rejects_a_malformed_email()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = "not-an-email" }).ToUrl("/users/me/email");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Value", error.Details.Keys);
    }

    private sealed record EmailResponseEnvelope(EmailResponse Email);

    private sealed record EmailResponse(string Value, bool IsVerified);
}
