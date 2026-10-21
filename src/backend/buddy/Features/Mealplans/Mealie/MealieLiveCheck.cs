using System.Globalization;

namespace buddy.Features.Mealplans;

// The live check behind ConnectMealie and TestMealieConnection: the instance answers, is new enough,
// accepts the token, and the token's user isn't a Mealie admin (docs/backend/analysis/
// mealie-integration.md, Questions 7 and 8).
public static class MealieLiveCheck
{
    // The oldest release whose API matches the 8 calls Buddy makes (checked against its OpenAPI spec).
    public static readonly Version MinimumVersion = new(3, 20, 0);

    public const string AdminRejectedMessage =
        "This token belongs to a Mealie admin. Create a separate Mealie user for Buddy without admin rights, and use its token.";

    public sealed record Passed(string Version, MealieUserDto User);

    public enum Field
    {
        BaseUrl,
        ApiToken
    }

    public sealed record Rejected(Field Field, MealieFailure Failure);

    public static async Task<(Passed? Passed, Rejected? Rejected)> RunAsync(IMealieClient client, MealieEndpoint endpoint, CancellationToken cancellationToken)
    {
        var aboutResponse = await client.GetAboutAsync(endpoint, cancellationToken);

        if (aboutResponse is not MealieResponse<MealieAboutDto>.Ok(var about))
        {
            return (null, new Rejected(Field.BaseUrl, aboutResponse.FailureOf()));
        }

        var version = about.Version?.Trim() ?? "";

        if (!IsSupported(version))
        {
            return (null, new Rejected(Field.BaseUrl, new MealieFailure(
                MealieFailureKind.Error,
                string.Create(CultureInfo.InvariantCulture, $"Buddy needs Mealie {MinimumVersion.ToString(3)} or newer; this server runs {version}. Update Mealie and try again."))));
        }

        return await client.GetSelfAsync(endpoint, cancellationToken) switch
        {
            MealieResponse<MealieUserDto>.Failed(var failure) =>
                (null, new Rejected(failure.Kind == MealieFailureKind.TokenRejected ? Field.ApiToken : Field.BaseUrl, failure)),
            MealieResponse<MealieUserDto>.Ok(var user) when user.Admin =>
                (null, new Rejected(Field.ApiToken, new MealieFailure(MealieFailureKind.TokenRejected, AdminRejectedMessage))),
            MealieResponse<MealieUserDto>.Ok(var user) => (new Passed(version, user), null),
        };
    }

    // "v3.29.0" and "3.29.0" parse; a build that doesn't name a version ("nightly", "develop") is
    // allowed, since it's newer than any release.
    public static bool IsSupported(string version)
    {
        var trimmed = version.TrimStart('v', 'V');
        var core = trimmed.Split('-', '+')[0];

        if (!Version.TryParse(core, out var parsed))
        {
            return trimmed.Length > 0 && !char.IsDigit(trimmed[0]);
        }

        return parsed >= MinimumVersion;
    }
}
