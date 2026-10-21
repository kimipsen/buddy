using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using buddy.Common;

namespace buddy.Features.Mealplans;

// Raw HTTP against the family's Mealie (no SDK, like the AI provider adapters). The HttpClient is
// the named "mealie" client, whose handler enforces MealieOutboundPolicy, has no redirects and caps
// the time and size of every response (MealplansFeature). Each call takes the endpoint because the
// base address is per family. Upstream bodies are never passed on, so nothing Buddy can't parse
// leaks back to a caller.
public sealed class MealieClient(HttpClient httpClient) : IMealieClient
{
    public const string HttpClientName = "mealie";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private const int MealPlanPageSize = 100;

    // Mealie's planner has no limit; this only stops a broken server from paging forever.
    private const int MaxMealPlanPages = 20;

    public Task<MealieResponse<MealieAboutDto>> GetAboutAsync(MealieEndpoint endpoint, CancellationToken cancellationToken) =>
        GetJsonAsync<MealieAboutDto>(endpoint, "/api/app/about", cancellationToken);

    public Task<MealieResponse<MealieUserDto>> GetSelfAsync(MealieEndpoint endpoint, CancellationToken cancellationToken) =>
        GetJsonAsync<MealieUserDto>(endpoint, "/api/users/self", cancellationToken);

    public Task<MealieResponse<MealiePageDto<MealieRecipeSummaryDto>>> SearchRecipesAsync(
        MealieEndpoint endpoint, string search, int page, int perPage, CancellationToken cancellationToken)
    {
        var query = string.Create(CultureInfo.InvariantCulture, $"page={page}&perPage={perPage}&orderBy=name&orderDirection=asc");

        if (search.Length > 0)
        {
            query += "&search=" + Uri.EscapeDataString(search);
        }

        return GetJsonAsync<MealiePageDto<MealieRecipeSummaryDto>>(endpoint, "/api/recipes?" + query, cancellationToken);
    }

    public Task<MealieResponse<MealieRecipeDto>> GetRecipeAsync(MealieEndpoint endpoint, Guid recipeId, CancellationToken cancellationToken) =>
        GetJsonAsync<MealieRecipeDto>(endpoint, $"/api/recipes/{recipeId}", cancellationToken);

    public async Task<MealieResponse<MealieImage>> GetRecipeImageAsync(MealieEndpoint endpoint, Guid recipeId, CancellationToken cancellationToken)
    {
        var sent = await SendAsync(endpoint, HttpMethod.Get, $"/api/media/recipes/{recipeId}/images/min-original.webp", null, cancellationToken);

        if (sent is not MealieResponse<HttpResponseMessage>.Ok(var response))
        {
            return Reraise<HttpResponseMessage, MealieImage>(sent);
        }

        using (response)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

            // Only ever pass an image through; anything else is reported, not proxied.
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Failed<MealieImage>(MealieFailureKind.Error, "Mealie didn't return an image.");
            }

            return await ReadAsync(
                async () => new MealieImage(await response.Content.ReadAsByteArrayAsync(cancellationToken), contentType));
        }
    }

    public async Task<MealieResponse<IReadOnlyList<MealiePlanEntryDto>>> ListMealPlanAsync(
        MealieEndpoint endpoint, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        List<MealiePlanEntryDto> entries = [];

        for (var page = 1; page <= MaxMealPlanPages; page++)
        {
            var path = string.Create(
                CultureInfo.InvariantCulture,
                $"/api/households/mealplans?start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}&page={page}&perPage={MealPlanPageSize}&orderBy=date&orderDirection=asc");
            var result = await GetJsonAsync<MealiePageDto<MealiePlanEntryDto>>(endpoint, path, cancellationToken);

            if (result is not MealieResponse<MealiePageDto<MealiePlanEntryDto>>.Ok(var current))
            {
                return Reraise<MealiePageDto<MealiePlanEntryDto>, IReadOnlyList<MealiePlanEntryDto>>(result);
            }

            entries.AddRange(current.Items ?? []);

            if (page >= current.TotalPages)
            {
                break;
            }
        }

        return new MealieResponse<IReadOnlyList<MealiePlanEntryDto>>.Ok(entries);
    }

    public async Task<MealieResponse<IReadOnlyList<MealieShoppingListDto>>> ListShoppingListsAsync(MealieEndpoint endpoint, CancellationToken cancellationToken)
    {
        var result = await GetJsonAsync<MealiePageDto<MealieShoppingListDto>>(
            endpoint, "/api/households/shopping/lists?page=1&perPage=-1&orderBy=name&orderDirection=asc", cancellationToken);

        return result switch
        {
            MealieResponse<MealiePageDto<MealieShoppingListDto>>.Ok(var page) =>
                new MealieResponse<IReadOnlyList<MealieShoppingListDto>>.Ok(page.Items ?? []),
            _ => Reraise<MealiePageDto<MealieShoppingListDto>, IReadOnlyList<MealieShoppingListDto>>(result),
        };
    }

    public async Task<MealieResponse<Unit>> AddRecipesToShoppingListAsync(
        MealieEndpoint endpoint, Guid listId, IReadOnlyList<MealieShoppingListRecipeDto> recipes, CancellationToken cancellationToken)
    {
        var sent = await SendAsync(
            endpoint, HttpMethod.Post, $"/api/households/shopping/lists/{listId}/recipe", JsonContent.Create(recipes, options: SerializerOptions), cancellationToken);

        if (sent is not MealieResponse<HttpResponseMessage>.Ok(var response))
        {
            return Reraise<HttpResponseMessage, Unit>(sent);
        }

        response.Dispose();

        return new MealieResponse<Unit>.Ok(Unit.Value);
    }

    private async Task<MealieResponse<T>> GetJsonAsync<T>(MealieEndpoint endpoint, string path, CancellationToken cancellationToken)
    {
        var sent = await SendAsync(endpoint, HttpMethod.Get, path, null, cancellationToken);

        if (sent is not MealieResponse<HttpResponseMessage>.Ok(var response))
        {
            return Reraise<HttpResponseMessage, T>(sent);
        }

        using (response)
        {
            return await ReadAsync(async () =>
                await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken)
                ?? throw new JsonException("Mealie returned an empty body."));
        }
    }

    private async Task<MealieResponse<HttpResponseMessage>> SendAsync(
        MealieEndpoint endpoint, HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(endpoint.BaseUrl + path, UriKind.Absolute)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        // SocketsHttpHandler wraps what ConnectCallback throws in an HttpRequestException.
        catch (HttpRequestException ex) when (ex.InnerException is MealieAddressBlockedException)
        {
            return new MealieResponse<HttpResponseMessage>.Failed(new MealieFailure(MealieFailureKind.Unreachable, MealieOutboundPolicy.PrivateAddressMessage));
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
        {
            // MaxResponseContentBufferSize exceeded.
            return Failed<HttpResponseMessage>(MealieFailureKind.Error, "Mealie's answer was too large.");
        }
        catch (HttpRequestException)
        {
            return Failed<HttpResponseMessage>(MealieFailureKind.Unreachable, "Buddy couldn't reach your Mealie.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed<HttpResponseMessage>(MealieFailureKind.Unreachable, "Your Mealie didn't answer in time.");
        }

        if (response.IsSuccessStatusCode)
        {
            return new MealieResponse<HttpResponseMessage>.Ok(response);
        }

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    Failed<HttpResponseMessage>(MealieFailureKind.TokenRejected, "Your Mealie rejected Buddy's API token."),
                HttpStatusCode.NotFound => Failed<HttpResponseMessage>(MealieFailureKind.NotFound, "Mealie couldn't find it."),
                >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest =>
                    Failed<HttpResponseMessage>(MealieFailureKind.Unreachable, "Your Mealie address redirects somewhere else. Use the address it redirects to."),
                _ => Failed<HttpResponseMessage>(MealieFailureKind.Error, string.Create(CultureInfo.InvariantCulture, $"Mealie answered with status {(int)response.StatusCode}.")),
            };
        }
    }

    private static async Task<MealieResponse<T>> ReadAsync<T>(Func<Task<T>> read)
    {
        try
        {
            return new MealieResponse<T>.Ok(await read());
        }
        catch (JsonException)
        {
            return Failed<T>(MealieFailureKind.Error, "Buddy couldn't read Mealie's answer. Is this a Mealie server?");
        }
        catch (NotSupportedException)
        {
            return Failed<T>(MealieFailureKind.Error, "Buddy couldn't read Mealie's answer. Is this a Mealie server?");
        }
    }

    private static MealieResponse<T> Failed<T>(MealieFailureKind kind, string message) =>
        new MealieResponse<T>.Failed(new MealieFailure(kind, message));

    private static MealieResponse<TOther> Reraise<T, TOther>(MealieResponse<T> response) => response switch
    {
        MealieResponse<T>.Failed(var failure) => new MealieResponse<TOther>.Failed(failure),
        MealieResponse<T>.Ok => throw new InvalidOperationException("Reraise called with a successful Mealie response."),
    };
}
