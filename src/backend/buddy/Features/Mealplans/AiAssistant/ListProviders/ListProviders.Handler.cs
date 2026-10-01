using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ListProvidersHandler
{
    public static async Task<Result<AiProviderSettings>> Handle(
        ListProviders query,
        IAiCredentialEventStore credentials,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<AiProviderSettings>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<AiProviderSettings>();
        }

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(query.ChildId, userId, guardians, credentials, cancellationToken);

        if (credentialId is null)
        {
            return new Result<AiProviderSettings>.Success(AiProviderSettings.Empty);
        }

        var credential = (await credentials.FindSnapshotAsync(credentialId, cancellationToken))!;

        return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(credential));
    }
}
