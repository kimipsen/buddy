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

    [Fact]
    public async Task Rejects_a_null_email()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = (string?)null }).ToUrl("/users/me/email");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        // Rejected while binding the body (RespectNullableAnnotations), keyed by the JSON field.
        Assert.Equal(["email"], error.Details.Keys);
    }

    [Fact]
    public async Task Accepts_an_email_of_200_characters()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var email = EmailOfLength(200);

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = email }).ToUrl("/users/me/email");
            _.StatusCodeShouldBeOk();
        });

        Assert.Equal(email, response.ReadAsJson<EmailResponseEnvelope>().Email.Value);
    }

    [Fact]
    public async Task Rejects_an_email_of_201_characters()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Patch.Json(new { Email = EmailOfLength(201) }).ToUrl("/users/me/email");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Contains("Value", error.Details.Keys);
    }

    // A unique, well-formed address of exactly `length` characters. The padding goes into
    // DNS-sized domain labels (at most 59 characters each) rather than the local part, so the
    // address stays deliverable to Mailpit.
    private static string EmailOfLength(int length)
    {
        var local = $"len-{Guid.NewGuid():N}@";
        const string tail = ".buddy.test";
        var remaining = length - local.Length - tail.Length;
        var labels = new List<string>();

        while (remaining > 60)
        {
            labels.Add(new string('a', 59));
            remaining -= 60;
        }

        labels.Add(new string('a', remaining));

        var email = local + string.Join(".", labels) + tail;
        Assert.Equal(length, email.Length);
        return email;
    }

    private sealed record EmailResponseEnvelope(EmailResponse Email);

    private sealed record EmailResponse(string Value, bool IsVerified);
}
