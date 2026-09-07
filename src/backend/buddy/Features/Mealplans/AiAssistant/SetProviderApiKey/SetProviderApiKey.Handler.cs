using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class SetProviderApiKeyHandler
{
    public static async Task<Result<AiProviderSettings>> Handle(
        SetProviderApiKey command,
        IValidator<SetProviderApiKey> validator,
        IAiCredentialEventStore credentials,
        IGuardianLinkEventStore guardians,
        IApiKeyCipher cipher,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<AiProviderSettings>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<AiProviderSettings>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<AiProviderSettings>();
        }

        var now = DateTimeOffset.UtcNow;
        var key = StoredApiKey.Create(command.ApiKey, cipher, userId, now);

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, guardians, credentials, cancellationToken);

        if (credentialId is null)
        {
            var newId = AiCredentialId.New();
            AiProviderCredentialEvent[] events =
            [
                new AiCredentialsInitialized(newId, command.ChildId, now),
                new ProviderApiKeySet(newId, command.Provider, key, now),
                // The family's very first key becomes the active provider automatically -- there's
                // otherwise no way to start a session without an extra "now pick one" step.
                new ActiveProviderChanged(newId, command.Provider, userId, now)
            ];

            await credentials.CreateAsync(newId, events, cancellationToken);

            return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(AiProviderCredential.Rehydrate(events)!));
        }

        var existingEvents = await credentials.ReadAsync(credentialId, cancellationToken);
        var existing = AiProviderCredential.Rehydrate(existingEvents)!;

        List<AiProviderCredentialEvent> newEvents = [new ProviderApiKeySet(credentialId, command.Provider, key, now)];

        if (existing.ActiveProvider is null)
        {
            newEvents.Add(new ActiveProviderChanged(credentialId, command.Provider, userId, now));
        }

        await credentials.AppendAsync(credentialId, newEvents, cancellationToken);

        var updated = AiProviderCredential.Rehydrate([.. existingEvents, .. newEvents])!;

        return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(updated));
    }
}
