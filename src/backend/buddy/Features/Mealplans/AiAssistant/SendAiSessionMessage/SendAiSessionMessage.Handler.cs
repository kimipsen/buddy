using System.Diagnostics;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class SendAiSessionMessageHandler
{
    // Caps provider round trips for a single reply so a bug (or an unusually stubborn model) can't
    // loop unboundedly against the family's own paid key -- see the AI mealplan plan.
    public const int MaxToolLoopIterations = 20;

    public const string NoMatchingMealsMessage = "No meals match this session's filter any more -- start a new session.";

    public static async Task<AiSessionOutcome> Handle(
        SendAiSessionMessage command,
        IValidator<SendAiSessionMessage> validator,
        IAiSessionEventStore sessions,
        IAiCredentialEventStore credentials,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IApiKeyCipher cipher,
        IAiProviderRegistry providerRegistry,
        ICalendarEventStore calendars,
        ICalendarItemEventStore calendarItems,
        ITaskTemplateEventStore taskTemplates,
        IGroupEventStore groups,
        ILogger<SendAiSessionMessage> logger,
        TimeProvider time,
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

        var sessionId = await AiSessionResolution.ResolveCurrentSessionIdAsync(command.ChildId, guardians, sessions, cancellationToken);

        if (sessionId is null)
        {
            return new AiSessionOutcome.NotFound();
        }

        var existingEvents = await sessions.ReadAsync(sessionId, cancellationToken);
        var session = MealplanAiSession.Replay(existingEvents);

        if (session.Status != AiSessionStatus.Drafting)
        {
            return new AiSessionOutcome.Validation(ValidationProblem.Of("This AI session is no longer active -- start a new one."));
        }

        var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, userId, guardians, credentials, cancellationToken);
        var credential = credentialId is null ? null : AiProviderCredential.Rehydrate(await credentials.ReadAsync(credentialId, cancellationToken));

        if (credential?.ActiveProvider is not { } activeProvider || !credential.Providers.TryGetValue(activeProvider, out var storedKey))
        {
            return new AiSessionOutcome.Validation(ValidationProblem.Of("No active AI provider is configured for this family."));
        }

        // A session started before acknowledgements existed must not keep sending data without one.
        if (credential.DataSharingAcknowledgedAt is null)
        {
            return new AiSessionOutcome.DataSharingNotAcknowledged();
        }

        IAiChatClient chatClient;

        try
        {
            chatClient = providerRegistry.Resolve(activeProvider);
        }
        catch (NotSupportedException ex)
        {
            logger.AiProviderUnsupported(ex, activeProvider);
            return new AiSessionOutcome.Validation(ValidationProblem.Of(ex.Message));
        }

        var apiKey = cipher.Unprotect(storedKey.CipherText);

        // Not existingEvents.OfType<AiSessionStarted>(): a union is a value type whose boxed
        // runtime type is always the union's own type, so the generic is-check OfType/Cast rely on
        // never matches a case type -- only pattern matching (switch/is, as below) understands
        // union cases.
        var started = existingEvents.Select(e => e switch { AiSessionStarted s => s, _ => null }).First(s => s is not null)!;

        // The prompt's meal list and the propose_assignment allowlist are the same filtered set,
        // so the server only accepts meals it actually offered.
        var selection = await AiMealFilter.LoadAsync(
            command.ChildId, started.From, started.To, started.RequestedSlots, started.RatedOnly, started.ServedWithin, started.MustIncludeMealIds,
            guardians, meals, mealPlans, cancellationToken);

        if (selection.FilterMatchedNothing)
        {
            // Keyed like StartAiSession's rejection, so the frontend can tell it from a provider error.
            return new AiSessionOutcome.Validation(new ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(AiSessionStarted.ServedWithin)] = [NoMatchingMealsMessage]
            }));
        }

        HashSet<MealId> offeredMealIds = [.. selection.Meals.Select(m => m.Id)];
        var systemPrompt = AiSessionPromptBuilder.Build(session, selection.Meals, started, selection.ReservedSlots);

        List<AiChatMessage> history = [.. AiSessionHistoryBuilder.Build(existingEvents), new AiChatMessage(AiChatMessageRole.User, command.Text, [])];
        List<MealplanAiSessionEvent> newEvents = [new AiUserMessageSent(sessionId, command.Text, userId, time.GetUtcNow())];

        var toolLoopResult = await RunToolLoopAsync(
            apiKey, chatClient, systemPrompt, history, newEvents, sessionId, session, offeredMealIds, selection.ReservedSlots, userId, started.ChildId,
            calendars, calendarItems, taskTemplates, groups, guardians, activeProvider, logger, time, cancellationToken);

        if (toolLoopResult is not Result<string>.Success(var finalText))
        {
            // RunToolLoopAsync fails only with Validation: the provider rejected the request.
            return toolLoopResult is Result<string>.Validation(var providerProblem)
                ? new AiSessionOutcome.Validation(providerProblem)
                : throw new UnreachableException($"Unexpected tool loop result: {toolLoopResult.GetType().Name}.");
        }

        newEvents.Add(new AiAssistantMessageRecorded(sessionId, finalText, time.GetUtcNow()));

        await sessions.AppendAsync(sessionId, newEvents, cancellationToken);

        MealplanAiSessionEvent[] allEvents = [.. existingEvents, .. newEvents];
        var updatedSession = MealplanAiSession.Replay(allEvents);
        var view = await AiSessionViewBuilder.BuildAsync(updatedSession, allEvents, meals, cancellationToken);

        return new AiSessionOutcome.Success(view);
    }

    // Drives the provider round-trip / tool-call turns, up to MaxToolLoopIterations. Mutates
    // history and newEvents in place (both are reference types the caller keeps using afterward);
    // the return value only carries the assistant's final reply text, or a Validation failure if
    // the provider itself rejected the request.
    private static async Task<Result<string>> RunToolLoopAsync(
        string apiKey,
        IAiChatClient chatClient,
        string systemPrompt,
        List<AiChatMessage> history,
        List<MealplanAiSessionEvent> newEvents,
        MealplanAiSessionId sessionId,
        MealplanAiSession session,
        IReadOnlyCollection<MealId> familyMealIds,
        IReadOnlyCollection<(DateOnly Date, MealSlot Slot)> reservedSlots,
        UserId userId,
        UserId sessionChildId,
        ICalendarEventStore calendars,
        ICalendarItemEventStore calendarItems,
        ITaskTemplateEventStore taskTemplates,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        AiProvider provider,
        ILogger logger,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var finalText = "";

        for (var iteration = 0; iteration < MaxToolLoopIterations; iteration++)
        {
            AiChatCompletionResult completion;

            try
            {
                completion = await chatClient.SendAsync(new AiChatCompletionRequest(apiKey, systemPrompt, history, AiSessionTools.Definitions), cancellationToken);
            }
            catch (AiProviderException ex)
            {
                logger.AiProviderRequestFailed(ex, provider, sessionId.Value, ex.StatusCode);
                return new Result<string>.Validation(ValidationProblem.Of($"The AI provider request failed: {ex.Message}"));
            }

            if (completion.ToolCalls.Count == 0)
            {
                finalText = completion.Text;
                break;
            }

            List<AiToolInvocation> turnInvocations = [];
            var now = time.GetUtcNow();

            foreach (var toolCall in completion.ToolCalls)
            {
                var outcome = await AiSessionToolExecutor.ExecuteAsync(
                    toolCall, sessionId, session, familyMealIds, reservedSlots, userId, sessionChildId, calendars, calendarItems, taskTemplates, groups, guardians, now, cancellationToken);

                newEvents.Add(new AiToolInvocationRecorded(sessionId, toolCall.ToolCallId, toolCall.ToolName, toolCall.ArgumentsJson, outcome.ResultJson, outcome.IsError, now));
                turnInvocations.Add(new AiToolInvocation(toolCall.ToolCallId, toolCall.ToolName, toolCall.ArgumentsJson, outcome.ResultJson, outcome.IsError));

                newEvents.AddRange(outcome.DraftEvents);
            }

            // The text accompanying tool calls ("Let me check the calendar...") is dropped, as it is
            // never persisted and so AiSessionHistoryBuilder replays the same shape. Kept, adapters
            // place it after the tool results, ending the request on an assistant turn: a prefill,
            // which Anthropic rejects with a 400.
            history.Add(new AiChatMessage(AiChatMessageRole.Assistant, "", turnInvocations));

            if (iteration == MaxToolLoopIterations - 1)
            {
                finalText = "I wasn't able to finish this within a reasonable number of steps -- here's what I've set so far. Feel free to ask me to continue.";
            }
        }

        return new Result<string>.Success(finalText);
    }
}
