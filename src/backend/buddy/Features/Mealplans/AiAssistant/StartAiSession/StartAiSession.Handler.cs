using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class StartAiSessionHandler
{
    public static async Task<Result<AiSessionView>> Handle(
        StartAiSession command,
        IValidator<StartAiSession> validator,
        IAiSessionEventStore sessions,
        IAiCredentialEventStore credentials,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<AiSessionView>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<AiSessionView>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<AiSessionView>();
        }

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, guardians, credentials, cancellationToken);

        if (credentialId is null)
        {
            return new Result<AiSessionView>.Validation(ValidationProblem.Of("No AI provider is configured for this family yet."));
        }

        var credential = AiProviderCredential.Rehydrate(await credentials.ReadAsync(credentialId, cancellationToken))!;

        if (credential.ActiveProvider is null)
        {
            return new Result<AiSessionView>.Validation(ValidationProblem.Of("No active AI provider is selected for this family yet."));
        }

        var now = DateTimeOffset.UtcNow;

        // Starting a new session supersedes whatever the family's current one is -- see
        // AiSessionResolution for why "current" means "latest across the family", not a single
        // mutable pointer that could go stale if a different sibling anchors the next session.
        var currentSessionId = await AiSessionResolution.ResolveCurrentSessionIdAsync(command.ChildId, guardians, sessions, cancellationToken);

        if (currentSessionId is { } existingId)
        {
            var existing = MealplanAiSession.Rehydrate(await sessions.ReadAsync(existingId, cancellationToken))!;

            if (existing.Status == AiSessionStatus.Drafting)
            {
                await sessions.AppendAsync(existingId, [new AiSessionDiscarded(existingId, userId, now)], cancellationToken);
            }
        }

        var newId = MealplanAiSessionId.New();
        MealplanAiSessionEvent[] events =
        [
            new AiSessionStarted(newId, command.ChildId, command.From, command.To, command.RequestedSlots, command.MustIncludeMealIds, command.Notes, userId, now)
        ];

        await sessions.CreateAsync(newId, events, cancellationToken);

        var session = MealplanAiSession.Rehydrate(events)!;
        var view = await AiSessionViewBuilder.BuildAsync(session, events, meals, cancellationToken);

        return new Result<AiSessionView>.Success(view);
    }
}
