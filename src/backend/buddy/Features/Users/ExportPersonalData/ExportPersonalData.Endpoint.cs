using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

using buddy.Common.RateLimiting;
using buddy.Features.Privacy;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

using Wolverine;

namespace buddy.Features.Users;

public static class ExportPersonalDataEndpoint
{
    public static RouteGroupBuilder MapExportPersonalData(this RouteGroupBuilder users)
    {
        users.MapGet("/me/export", async Task<FileContentHttpResult> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            IOptions<JsonOptions> json,
            CancellationToken cancellationToken) =>
        {
            var document = await bus.InvokeAsync<PersonalDataExportDocument>(ExportPersonalData.FromClaims(principal), cancellationToken);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, ExportOptions(json.Value.SerializerOptions));
            var fileName = $"buddy-export-{document.ExportedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.json";

            return TypedResults.File(bytes, "application/json", fileName);
        })
        .RequireRateLimiting(RateLimitingFeature.PersonalDataExportPolicy)
        .WithName("ExportPersonalData");

        return users;
    }

    // Meant to be read by a person or another service, not by the Buddy frontend: indented, enums by
    // name. Nullable annotations aren't enforced on the way out -- a value a snapshot never set
    // shouldn't fail the whole export.
    private static JsonSerializerOptions ExportOptions(JsonSerializerOptions http)
    {
        var options = new JsonSerializerOptions(http)
        {
            WriteIndented = true,
            RespectNullableAnnotations = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
