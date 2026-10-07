using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Records that a guardian has read what the assistant sends to the family's provider (GDPR
// Question 6.3). Once per family: acknowledging again changes nothing.
public static class AcknowledgeAiDataSharingHandler
{
    public static async Task<Result<AiProviderSettings>> Handle(
        AcknowledgeAiDataSharing command,
        IAiCredentialEventStore credentials,
        IGuardianLinkEventStore guardians,
        ILogger<AcknowledgeAiDataSharing> logger,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<AiProviderSettings>();
        }

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, userId, guardians, credentials, cancellationToken);

        if (credentialId is null)
        {
            return new Result<AiProviderSettings>.Validation(ValidationProblem.Of("No AI provider is configured for this family yet."));
        }

        var events = await credentials.ReadAsync(credentialId, cancellationToken);
        var credential = AiProviderCredential.Replay(events);

        if (credential.DataSharingAcknowledgedAt is not null)
        {
            return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(credential));
        }

        AiProviderCredentialEvent[] acknowledged = [new AiDataSharingAcknowledged(credentialId, userId, DateTimeOffset.UtcNow)];
        await credentials.AppendAsync(credentialId, acknowledged, cancellationToken);

        logger.AiDataSharingAcknowledged(command.ChildId.Value, userId.Value);

        return new Result<AiProviderSettings>.Success(AiProviderSettings.FromCredential(AiProviderCredential.Replay([.. events, .. acknowledged])));
    }
}
