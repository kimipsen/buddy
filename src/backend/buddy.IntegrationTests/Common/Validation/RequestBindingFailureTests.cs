using Alba;

using buddy.Common;
using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Common.Validation;

// A body the framework can't bind (RespectRequiredConstructorParameters / RespectNullableAnnotations
// on the HTTP JSON options) comes back as the same ErrorEnvelope a validator failure produces --
// see RequestBindingFailureMiddleware.
[Collection(BuddyApiCollection.Name)]
public sealed class RequestBindingFailureTests(BuddyApiFixture fixture)
{
    private const string AddLocationUrl = "/work-locations/me/locations";

    [Fact]
    public async Task An_omitted_required_field_is_a_validation_error_naming_the_field()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Icon = "🏢", Color = "#2563eb" }).ToUrl(AddLocationUrl);
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["name"], error.Details.Keys);
        Assert.False(string.IsNullOrEmpty(error.RequestId));
    }

    [Fact]
    public async Task An_explicit_null_for_a_non_nullable_field_is_a_validation_error_at_its_path()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Stil", Icon = (string?)null, Color = "#2563eb" }).ToUrl(AddLocationUrl);
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["icon"], error.Details.Keys);
    }

    [Fact]
    public async Task A_missing_field_inside_a_list_element_is_reported_with_its_element_path()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Json(new
            {
                CycleWeeks = 1,
                AnchorMonday = new DateOnly(2026, 9, 28),
                Days = new object[] { new { Week = 1, Day = DayOfWeek.Monday } },
            }).ToUrl("/work-locations/me/pattern");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        Assert.Equal(["days[0].locationId"], error.Details.Keys);
    }

    [Fact]
    public async Task Malformed_json_is_a_validation_error()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Url(AddLocationUrl);
            _.ConfigureHttpContext(context =>
            {
                context.Request.ContentType = "application/json";
                context.Request.Body = new MemoryStream("{\"name\": "u8.ToArray());
            });
            _.StatusCodeShouldBe(400);
        });

        Assert.Equal("validation_error", response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task An_unbindable_query_value_is_a_validation_error_without_echoing_the_input()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/events?pageSize=abc");
            _.StatusCodeShouldBe(400);
        });

        var error = response.ReadAsJson<ErrorEnvelope>();
        Assert.Equal("validation_error", error.Code);
        var message = Assert.Single(Assert.Single(error.Details.Values));
        Assert.Equal("A route, query or body value is missing or malformed.", message);
        Assert.DoesNotContain("abc", response.ReadAsText());
    }

    [Fact]
    public async Task An_omitted_optional_field_still_binds()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Post.Json(new { Name = "Ugeskabelon" }).ToUrl("/print-templates");
            _.StatusCodeShouldBeOk();
        });
    }
}
