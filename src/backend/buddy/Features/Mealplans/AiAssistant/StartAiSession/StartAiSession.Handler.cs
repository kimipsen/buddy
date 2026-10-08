using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class StartAiSessionHandler
{
    public const string NoMatchingMealsMessage = "No meals match this filter. Choose a longer period or include unrated meals.";

    public static async Task<AiSessionOutcome> Handle(
        StartAiSession command,
        IValidator<StartAiSession> validator,
        IAiSessionEventStore sessions,
        IAiCredentialEventStore credentials,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new AiSessionOutcome.Validation(problem);
        }

        var userId = command.UserId;

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return AiSessionOutcome.Denied(access);
        }

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, userId, guardians, credentials, cancellationToken);

        if (credentialId is null)
        {
            return new AiSessionOutcome.Validation(ValidationProblem.Of("No AI provider is configured for this family yet."));
        }

        var credential = AiProviderCredential.Replay(await credentials.ReadAsync(credentialId, cancellationToken));

        if (credential.ActiveProvider is null)
        {
            return new AiSessionOutcome.Validation(ValidationProblem.Of("No active AI provider is selected for this family yet."));
        }

        if (credential.DataSharingAcknowledgedAt is null)
        {
            return new AiSessionOutcome.DataSharingNotAcknowledged();
        }

        // Checked before anything is created or discarded, so a filter that matches nothing leaves
        // the family's current session untouched and sends nothing to the provider.
        if (AiMealFilter.IsActive(command.RatedOnly, command.ServedWithin))
        {
            var selection = await AiMealFilter.LoadAsync(
                command.ChildId, command.From, command.RatedOnly, command.ServedWithin, command.MustIncludeMealIds,
                guardians, meals, mealPlans, cancellationToken);

            if (selection.FilterMatchedNothing)
            {
                return new AiSessionOutcome.Validation(new ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(StartAiSession.ServedWithin)] = [NoMatchingMealsMessage]
                }));
            }
        }

        var now = DateTimeOffset.UtcNow;

        // Starting a new session supersedes whatever the family's current one is -- see
        // AiSessionResolution for why "current" means "latest across the family", not a single
        // mutable pointer that could go stale if a different sibling anchors the next session.
        var currentSessionId = await AiSessionResolution.ResolveCurrentSessionIdAsync(command.ChildId, guardians, sessions, cancellationToken);

        if (currentSessionId is { } existingId)
        {
            var existing = MealplanAiSession.Replay(await sessions.ReadAsync(existingId, cancellationToken));

            if (existing.Status == AiSessionStatus.Drafting)
            {
                await sessions.AppendAsync(existingId, [new AiSessionDiscarded(existingId, userId, now)], cancellationToken);
            }
        }

        var newId = MealplanAiSessionId.New();
        MealplanAiSessionEvent[] events =
        [
            new AiSessionStarted(newId, command.ChildId, command.From, command.To, command.RequestedSlots, command.MustIncludeMealIds, command.Notes, userId, now,
                command.RatedOnly, command.ServedWithin)
        ];

        await sessions.CreateAsync(newId, events, cancellationToken);

        var session = MealplanAiSession.Replay(events);
        var view = await AiSessionViewBuilder.BuildAsync(session, events, meals, cancellationToken);

        return new AiSessionOutcome.Success(view);
    }
}
