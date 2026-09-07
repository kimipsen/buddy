using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class RemoveProviderApiKeyHandler
{
    public static async Task<Result<AiProviderSettings>> Handle(
        RemoveProviderApiKey command,
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
            return new Result<AiProviderSettings>.Success(AiProviderSettings.Empty);
        }

        var existingEvents = await credentials.ReadAsync(credentialId, cancellationToken);
        var existing = AiProviderCredential.Rehydrate(existingEvents)!;

        if (!existing.Providers.ContainsKey(command.Provider))
        {
            // Removing a key that isn't there is idempotent, same rationale as ClearMealSlot
            // tolerating a double-tap clear.
            return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(existing));
        }

        var now = DateTimeOffset.UtcNow;
        List<AiProviderCredentialEvent> newEvents = [new ProviderApiKeyRemoved(credentialId, command.Provider, userId, now)];

        if (existing.ActiveProvider == command.Provider)
        {
            newEvents.Add(new ActiveProviderChanged(credentialId, null, userId, now));
        }

        await credentials.AppendAsync(credentialId, newEvents, cancellationToken);

        var updated = AiProviderCredential.Rehydrate([.. existingEvents, .. newEvents])!;

        return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(updated));
    }
}
