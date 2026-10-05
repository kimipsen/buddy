namespace buddy.Common.Versioning;

public static class VersionEndpoint
{
    // Anonymous like /health: lets a deploy check (and anyone debugging) see which build is live.
    public static IEndpointRouteBuilder MapVersion(this IEndpointRouteBuilder app)
    {
        app.MapGet("/version", () => TypedResults.Ok(BuildVersion.Current))
            .WithName("GetVersion")
            .WithTags("Version")
            .AllowAnonymous();

        return app;
    }
}
