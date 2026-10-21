using System.Net;
using System.Text;

using buddy.Features.Mealplans;

using Xunit;

namespace buddy.IntegrationTests.Features.Mealplans.Mealie;

// The real MealieClient over a stubbed HttpMessageHandler: the requests it sends and how it reads
// every kind of answer. The other Mealie tests swap in FakeMealieClient, and MealieContractTests
// (opt-in) runs this client against a real Mealie. No host, no database.
public sealed class MealieClientTests
{
    private const string BaseUrl = "https://mealie.example.com";
    private static readonly MealieEndpoint Endpoint = new(BaseUrl, "token-1234");

    [Fact]
    public async Task Every_request_carries_the_token_and_asks_for_JSON()
    {
        var (client, handler) = Create(_ => Json("""{"version":"v3.29.0"}"""));

        var about = await client.GetAboutAsync(Endpoint, CancellationToken.None);

        Assert.Equal("v3.29.0", Ok(about).Version);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{BaseUrl}/api/app/about", request.Url);
        Assert.Equal("Bearer token-1234", request.Authorization);
        Assert.Equal("application/json", request.Accept);
    }

    [Fact]
    public async Task Each_call_reaches_its_Mealie_route()
    {
        var recipeId = Guid.Parse("6b1d4c2e-0000-4000-8000-000000000001");
        var listId = Guid.Parse("6b1d4c2e-0000-4000-8000-000000000002");
        var (client, handler) = Create(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/users/self" => Json($$"""{"id":"{{Guid.Empty}}","username":"buddy","admin":false,"household":"Holm","householdId":"{{Guid.Empty}}","groupId":"{{Guid.Empty}}","groupSlug":"holm"}"""),
            "/api/recipes" => Json("""{"page":1,"per_page":20,"total":0,"total_pages":1,"items":[]}"""),
            "/api/households/shopping/lists" => Json($$"""{"page":1,"per_page":-1,"total":1,"total_pages":1,"items":[{"id":"{{listId}}","name":"Weekly shop"}]}"""),
            _ when request.Method == HttpMethod.Post => new HttpResponseMessage(HttpStatusCode.OK),
            _ => Json($$"""{"id":"{{recipeId}}","slug":"lasagne","name":"Lasagne"}"""),
        });

        Assert.Equal("buddy", Ok(await client.GetSelfAsync(Endpoint, CancellationToken.None)).Username);
        Ok(await client.SearchRecipesAsync(Endpoint, "", 2, 20, CancellationToken.None));
        Ok(await client.SearchRecipesAsync(Endpoint, "mac & cheese", 1, 20, CancellationToken.None));
        Assert.Equal("lasagne", Ok(await client.GetRecipeAsync(Endpoint, recipeId, CancellationToken.None)).Slug);
        Assert.Equal(new MealieShoppingListDto(listId, "Weekly shop"), Assert.Single(Ok(await client.ListShoppingListsAsync(Endpoint, CancellationToken.None))));
        Ok(await client.AddRecipesToShoppingListAsync(Endpoint, listId, [new MealieShoppingListRecipeDto(recipeId, 2)], CancellationToken.None));

        Assert.Equal(
            [
                $"GET {BaseUrl}/api/users/self",
                $"GET {BaseUrl}/api/recipes?page=2&perPage=20&orderBy=name&orderDirection=asc",
                $"GET {BaseUrl}/api/recipes?page=1&perPage=20&orderBy=name&orderDirection=asc&search=mac%20%26%20cheese",
                $"GET {BaseUrl}/api/recipes/{recipeId}",
                $"GET {BaseUrl}/api/households/shopping/lists?page=1&perPage=-1&orderBy=name&orderDirection=asc",
                $"POST {BaseUrl}/api/households/shopping/lists/{listId}/recipe",
            ],
            handler.Requests.Select(r => $"{r.Method} {r.Url}"));
        Assert.Equal($$"""[{"recipeId":"{{recipeId}}","recipeIncrementQuantity":2}]""", handler.Requests[^1].Body);
    }

    [Fact]
    public async Task A_shopping_list_page_without_items_is_an_empty_list()
    {
        var (client, _) = Create(_ => Json("""{"page":1,"per_page":-1,"total":0,"total_pages":0,"items":null}"""));

        Assert.Empty(Ok(await client.ListShoppingListsAsync(Endpoint, CancellationToken.None)));
    }

    [Fact]
    public async Task The_meal_plan_is_read_page_by_page_until_the_last()
    {
        var (client, handler) = Create(request =>
        {
            var page = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["page"]!, System.Globalization.CultureInfo.InvariantCulture);
            return Json($$"""{"page":{{page}},"per_page":100,"total":3,"total_pages":3,"items":[{"id":{{page}},"date":"2026-10-2{{page}}","entryType":"dinner","title":"Meal {{page}}"}]}""");
        });

        var entries = Ok(await client.ListMealPlanAsync(Endpoint, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), CancellationToken.None));

        Assert.Equal([1, 2, 3], entries.Select(e => e.Id));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(
            $"{BaseUrl}/api/households/mealplans?start_date=2026-10-19&end_date=2026-10-25&page=1&perPage=100&orderBy=date&orderDirection=asc",
            handler.Requests[0].Url);
    }

    [Fact]
    public async Task A_meal_plan_that_never_ends_stops_after_twenty_pages()
    {
        var (client, handler) = Create(_ => Json("""{"page":1,"per_page":100,"total":99999,"total_pages":999,"items":[]}"""));

        Ok(await client.ListMealPlanAsync(Endpoint, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), CancellationToken.None));

        Assert.Equal(20, handler.Requests.Count);
    }

    [Fact]
    public async Task A_failed_meal_plan_page_fails_the_whole_read()
    {
        var calls = 0;
        var (client, _) = Create(_ => ++calls == 1
            ? Json("""{"page":1,"per_page":100,"total":200,"total_pages":2,"items":[]}""")
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await client.ListMealPlanAsync(Endpoint, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), CancellationToken.None);

        Assert.Equal(new MealieFailure(MealieFailureKind.Error, "Mealie answered with status 500."), result.FailureOf());
    }

    [Fact]
    public async Task A_recipe_photo_is_passed_on_with_its_type()
    {
        var recipeId = Guid.Parse("6b1d4c2e-0000-4000-8000-000000000003");
        var (client, handler) = Create(_ => Bytes([1, 2, 3], "image/webp"));

        var image = Ok(await client.GetRecipeImageAsync(Endpoint, recipeId, CancellationToken.None));

        Assert.Equal([1, 2, 3], image.Content);
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal($"{BaseUrl}/api/media/recipes/{recipeId}/images/min-original.webp", Assert.Single(handler.Requests).Url);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData(null)]
    public async Task Anything_but_an_image_is_never_passed_on_as_a_photo(string? contentType)
    {
        var (client, _) = Create(_ => Bytes(Encoding.UTF8.GetBytes("<html></html>"), contentType));

        var image = await client.GetRecipeImageAsync(Endpoint, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(new MealieFailure(MealieFailureKind.Error, "Mealie didn't return an image."), image.FailureOf());
    }

    [Fact]
    public async Task A_failed_photo_or_shopping_call_reports_why()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var image = await client.GetRecipeImageAsync(Endpoint, Guid.NewGuid(), CancellationToken.None);
        var added = await client.AddRecipesToShoppingListAsync(Endpoint, Guid.NewGuid(), [], CancellationToken.None);
        var lists = await client.ListShoppingListsAsync(Endpoint, CancellationToken.None);

        Assert.Equal(MealieFailureKind.NotFound, image.FailureOf().Kind);
        Assert.Equal(MealieFailureKind.NotFound, added.FailureOf().Kind);
        Assert.Equal(MealieFailureKind.NotFound, lists.FailureOf().Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, MealieFailureKind.TokenRejected, "Your Mealie rejected Buddy's API token.")]
    [InlineData(HttpStatusCode.Forbidden, MealieFailureKind.TokenRejected, "Your Mealie rejected Buddy's API token.")]
    [InlineData(HttpStatusCode.NotFound, MealieFailureKind.NotFound, "Mealie couldn't find it.")]
    [InlineData(HttpStatusCode.MultipleChoices, MealieFailureKind.Unreachable, "Your Mealie address redirects somewhere else. Use the address it redirects to.")]
    [InlineData(HttpStatusCode.Found, MealieFailureKind.Unreachable, "Your Mealie address redirects somewhere else. Use the address it redirects to.")]
    [InlineData((HttpStatusCode)399, MealieFailureKind.Unreachable, "Your Mealie address redirects somewhere else. Use the address it redirects to.")]
    [InlineData(HttpStatusCode.BadRequest, MealieFailureKind.Error, "Mealie answered with status 400.")]
    [InlineData(HttpStatusCode.InternalServerError, MealieFailureKind.Error, "Mealie answered with status 500.")]
    public async Task An_unsuccessful_status_becomes_a_failure_a_guardian_can_act_on(HttpStatusCode status, MealieFailureKind kind, string message)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(status) { Content = new StringContent("""{"detail":"secret upstream text"}""") });

        var about = await client.GetAboutAsync(Endpoint, CancellationToken.None);

        Assert.Equal(new MealieFailure(kind, message), about.FailureOf());
    }

    [Fact]
    public async Task Failures_on_the_way_become_unreachable_or_too_large()
    {
        Assert.Equal(
            new MealieFailure(MealieFailureKind.Unreachable, "Buddy couldn't reach your Mealie."),
            await FailureOfThrowingAsync(new HttpRequestException("connection refused")));
        Assert.Equal(
            new MealieFailure(MealieFailureKind.Error, "Mealie's answer was too large."),
            await FailureOfThrowingAsync(new HttpRequestException(HttpRequestError.ConfigurationLimitExceeded, "too big")));
        Assert.Equal(
            new MealieFailure(MealieFailureKind.Unreachable, MealieOutboundPolicy.PrivateAddressMessage),
            await FailureOfThrowingAsync(new HttpRequestException("blocked", new MealieAddressBlockedException("nas.local"))));
        Assert.Equal(
            new MealieFailure(MealieFailureKind.Unreachable, "Your Mealie didn't answer in time."),
            await FailureOfThrowingAsync(new TaskCanceledException("timeout")));
    }

    [Fact]
    public async Task A_cancelled_request_is_cancelled_not_reported_as_a_timeout()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var (client, _) = Create(_ => throw new TaskCanceledException("cancelled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAboutAsync(Endpoint, cancelled.Token));
    }

    [Theory]
    [InlineData("""{"version":""", "application/json")]
    [InlineData("null", "application/json")]
    [InlineData("<html>Not Mealie</html>", "text/html")]
    public async Task An_answer_that_isnt_Mealie_JSON_is_reported_not_thrown(string body, string contentType)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, contentType) });

        var about = await client.GetAboutAsync(Endpoint, CancellationToken.None);

        Assert.Equal(new MealieFailure(MealieFailureKind.Error, "Buddy couldn't read Mealie's answer. Is this a Mealie server?"), about.FailureOf());
    }

    private static async Task<MealieFailure> FailureOfThrowingAsync(Exception exception)
    {
        var (client, _) = Create(_ => throw exception);

        return (await client.GetAboutAsync(Endpoint, CancellationToken.None)).FailureOf();
    }

    private static T Ok<T>(MealieResponse<T> response) =>
        Assert.IsType<MealieResponse<T>.Ok>(response.Value).Value;

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(byte[] body, string? contentType)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = contentType is null ? null : new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static (MealieClient Client, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);

        return (new MealieClient(new HttpClient(handler)), handler);
    }

    private sealed record SentRequest(HttpMethod Method, string Url, string? Authorization, string? Accept, string? Body);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.Headers.Authorization?.ToString(),
                string.Join(",", request.Headers.Accept),
                body));

            return respond(request);
        }
    }
}
