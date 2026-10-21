using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

using Microsoft.Extensions.Options;

namespace buddy.Features.Mealplans;

// Saves a connection only after it worked: the outbound policy accepts the address, the instance
// answers and is new enough, the token is accepted and isn't an admin's. Every failure is a
// validation problem on the field to fix. See docs/backend/analysis/mealie-integration.md, Question 2.
public static class ConnectMealieHandler
{
    public static async Task<Result<MealieConnectionView>> Handle(
        ConnectMealie command,
        IValidator<ConnectMealie> validator,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        IOptions<MealieOptions> options,
        ILogger<ConnectMealie> logger,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MealieConnectionView>.Validation(problem);
        }

        var userId = command.UserId;
        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<MealieConnectionView>();
        }

        var (baseUrl, urlError) = MealieOutboundPolicy.NormalizeBaseUrl(command.BaseUrl, options.Value);

        if (baseUrl is null)
        {
            return FieldProblem("baseUrl", urlError ?? "");
        }

        var (passed, rejected) = await MealieLiveCheck.RunAsync(mealie, new MealieEndpoint(baseUrl, command.ApiToken), cancellationToken);

        if (passed is null)
        {
            logger.MealieConnectRejected(command.ChildId.Value, userId.Value, rejected?.Failure.Kind ?? MealieFailureKind.Error);

            return FieldProblem(rejected?.Field == MealieLiveCheck.Field.ApiToken ? "apiToken" : "baseUrl", rejected?.Failure.Message ?? "");
        }

        var now = time.GetUtcNow();
        var user = passed.User;
        var household = new MealieHousehold(user.HouseholdId, user.Household ?? "", user.GroupId, user.GroupSlug ?? "");
        var token = StoredApiKey.Create(command.ApiToken, cipher, userId, now);

        var id = await MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync(command.ChildId, guardians, connections, cancellationToken);
        MealieConnection connection;

        if (id is null)
        {
            var newId = MealieConnectionId.New();
            MealieConnectionEvent[] events =
            [
                new MealieConnectionInitialized(newId, command.ChildId, now),
                new MealieConnected(newId, baseUrl, token, household, user.Username ?? "", passed.Version, userId, now)
            ];

            connection = MealieConnection.Replay(await connections.CreateAsync(newId, events, cancellationToken));
        }
        else
        {
            var existing = await connections.ReadAsync(id, cancellationToken);
            MealieConnectionEvent connected = new MealieConnected(id, baseUrl, token, household, user.Username ?? "", passed.Version, userId, now);

            await connections.AppendAsync(id, [connected], cancellationToken);
            connection = MealieConnection.Replay([.. existing, connected]);
        }

        logger.MealieConnected(command.ChildId.Value, userId.Value);

        return new Result<MealieConnectionView>.Success(MealieConnectionView.From(connection));
    }

    private static Result<MealieConnectionView> FieldProblem(string field, string message) =>
        new Result<MealieConnectionView>.Validation(new ValidationProblem(new Dictionary<string, string[]> { [field] = [message] }));
}
