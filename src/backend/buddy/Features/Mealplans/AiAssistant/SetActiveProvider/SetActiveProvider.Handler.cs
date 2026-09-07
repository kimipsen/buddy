using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class SetActiveProviderHandler
{
    public static async Task<Result<AiProviderSettings>> Handle(
        SetActiveProvider command,
        IAiCredentialEventStore credentials,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<AiProviderSettings>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<AiProviderSettings>();
        }

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, guardians, credentials, cancellationToken);

        if (credentialId is null)
        {
            // State-dependent (needs to know whether a key exists at all), so this stays as
            // handler code rather than a FluentValidation rule -- same reasoning as
            // AssignMealToSlotHandler's archived-meal check.
            return new Result<AiProviderSettings>.Validation(ValidationProblem.Of("No AI provider key has been added yet."));
        }

        var existingEvents = await credentials.ReadAsync(credentialId, cancellationToken);
        var existing = AiProviderCredential.Rehydrate(existingEvents)!;

        if (!existing.Providers.ContainsKey(command.Provider))
        {
            return new Result<AiProviderSettings>.Validation(ValidationProblem.Of($"No API key has been added for {command.Provider} yet."));
        }

        if (existing.ActiveProvider == command.Provider)
        {
            return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(existing));
        }

        var changed = new ActiveProviderChanged(credentialId, command.Provider, userId, DateTimeOffset.UtcNow);
        await credentials.AppendAsync(credentialId, [changed], cancellationToken);

        return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(existing with { ActiveProvider = command.Provider }));
    }
}
