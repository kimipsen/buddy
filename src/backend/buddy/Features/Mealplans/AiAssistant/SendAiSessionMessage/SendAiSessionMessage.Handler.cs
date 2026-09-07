using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.TaskLibrary;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class SendAiSessionMessageHandler
{
    // Caps provider round trips for a single reply so a bug (or an unusually stubborn model) can't
    // loop unboundedly against the family's own paid key -- see the AI mealplan plan.
    public const int MaxToolLoopIterations = 20;

    public static async Task<Result<AiSessionView>> Handle(
        SendAiSessionMessage command,
        IValidator<SendAiSessionMessage> validator,
        IAiSessionEventStore sessions,
        IAiCredentialEventStore credentials,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IApiKeyCipher cipher,
        IAiProviderRegistry providerRegistry,
        ICalendarEventStore calendars,
        ICalendarItemEventStore calendarItems,
        ITaskTemplateEventStore taskTemplates,
        IGroupEventStore groups,
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

        var sessionId = await AiSessionResolution.ResolveCurrentSessionIdAsync(command.ChildId, guardians, sessions, cancellationToken);

        if (sessionId is null)
        {
            return new Result<AiSessionView>.NotFound();
        }

        var existingEvents = await sessions.ReadAsync(sessionId, cancellationToken);
        var session = MealplanAiSession.Rehydrate(existingEvents)!;

        if (session.Status != AiSessionStatus.Drafting)
        {
            return new Result<AiSessionView>.Validation(ValidationProblem.Of("This AI session is no longer active -- start a new one."));
        }

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, guardians, credentials, cancellationToken);
        var credential = credentialId is null ? null : AiProviderCredential.Rehydrate(await credentials.ReadAsync(credentialId, cancellationToken));

        if (credential?.ActiveProvider is not { } activeProvider || !credential.Providers.TryGetValue(activeProvider, out var storedKey))
        {
            return new Result<AiSessionView>.Validation(ValidationProblem.Of("No active AI provider is configured for this family."));
        }

        IAiChatClient chatClient;

        try
        {
            chatClient = providerRegistry.Resolve(activeProvider);
        }
        catch (NotSupportedException ex)
        {
            return new Result<AiSessionView>.Validation(ValidationProblem.Of(ex.Message));
        }

        var apiKey = cipher.Unprotect(storedKey.CipherText);

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(command.ChildId, guardians, meals, cancellationToken);
        List<Meal> familyMeals = [];

        foreach (var mealId in familyMealIds)
        {
            if (Meal.Rehydrate(await meals.ReadAsync(mealId, cancellationToken)) is { } meal)
            {
                familyMeals.Add(meal);
            }
        }

        // Not existingEvents.OfType<AiSessionStarted>(): a union is a value type whose boxed
        // runtime type is always the union's own type, so the generic is-check OfType/Cast rely on
        // never matches a case type -- only pattern matching (switch/is, as below) understands
        // union cases.
        var started = existingEvents.Select(e => e switch { AiSessionStarted s => s, _ => null }).First(s => s is not null)!;
        var systemPrompt = AiSessionPromptBuilder.Build(session, familyMeals, started.MustIncludeMealIds, started.Notes);

        List<AiChatMessage> history = [.. AiSessionHistoryBuilder.Build(existingEvents), new AiChatMessage(AiChatMessageRole.User, command.Text, [])];
        List<MealplanAiSessionEvent> newEvents = [new AiUserMessageSent(sessionId, command.Text, userId, DateTimeOffset.UtcNow)];
        string? finalText = null;

        for (var iteration = 0; iteration < MaxToolLoopIterations; iteration++)
        {
            AiChatCompletionResult completion;

            try
            {
                completion = await chatClient.SendAsync(new AiChatCompletionRequest(apiKey, systemPrompt, history, AiSessionTools.Definitions), cancellationToken);
            }
            catch (AiProviderException ex)
            {
                return new Result<AiSessionView>.Validation(ValidationProblem.Of($"The AI provider request failed: {ex.Message}"));
            }

            if (completion.ToolCalls.Count == 0)
            {
                finalText = completion.Text ?? "";
                break;
            }

            List<AiToolInvocation> turnInvocations = [];
            var now = DateTimeOffset.UtcNow;

            foreach (var toolCall in completion.ToolCalls)
            {
                var outcome = await AiSessionToolExecutor.ExecuteAsync(
                    toolCall, sessionId, session, familyMealIds, userId, calendars, calendarItems, taskTemplates, groups, guardians, now, cancellationToken);

                newEvents.Add(new AiToolInvocationRecorded(sessionId, toolCall.ToolCallId, toolCall.ToolName, toolCall.ArgumentsJson, outcome.ResultJson, outcome.IsError, now));
                turnInvocations.Add(new AiToolInvocation(toolCall.ToolCallId, toolCall.ToolName, toolCall.ArgumentsJson, outcome.ResultJson, outcome.IsError));

                if (outcome.DraftEvent is { } draftEvent)
                {
                    newEvents.Add(draftEvent);
                }
            }

            history.Add(new AiChatMessage(AiChatMessageRole.Assistant, completion.Text, turnInvocations));

            if (iteration == MaxToolLoopIterations - 1)
            {
                finalText = "I wasn't able to finish this within a reasonable number of steps -- here's what I've set so far. Feel free to ask me to continue.";
            }
        }

        newEvents.Add(new AiAssistantMessageRecorded(sessionId, finalText ?? "", DateTimeOffset.UtcNow));

        await sessions.AppendAsync(sessionId, newEvents, cancellationToken);

        MealplanAiSessionEvent[] allEvents = [.. existingEvents, .. newEvents];
        var updatedSession = MealplanAiSession.Rehydrate(allEvents)!;
        var view = await AiSessionViewBuilder.BuildAsync(updatedSession, allEvents, meals, cancellationToken);

        return new Result<AiSessionView>.Success(view);
    }
}
