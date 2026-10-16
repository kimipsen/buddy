namespace buddy.Features.TaskLibrary;

// The shared tail of withdraw, approve and decline, after authorization: an unknown suggestion is
// 404; repeating the same resolution is an idempotent 200 with no new event (an approval repeat
// ignores its body); moving a suggestion that is already resolved some other way is 409
// suggestion_resolved (docs/backend/analysis/packing-checklists.md, Question 8).
internal static class ChecklistSuggestionResolution
{
    public static async Task<ChecklistSuggestionOutcome> ResolveAsync(
        ResolvedTaskTemplate loaded,
        ChecklistSuggestionId suggestionId,
        ChecklistSuggestionStatus target,
        Func<DateTimeOffset, TaskTemplateEvent[]> events,
        ITaskTemplateEventStore templates,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (loaded.Template.FindSuggestion(suggestionId) is not { } suggestion)
        {
            return new ChecklistSuggestionOutcome.NotFound();
        }

        if (suggestion.Status == target)
        {
            return new ChecklistSuggestionOutcome.Success(loaded.Template);
        }

        if (!suggestion.IsPending)
        {
            return new ChecklistSuggestionOutcome.AlreadyResolved();
        }

        var newEvents = events(time.GetUtcNow());

        await templates.AppendAsync(loaded.Template.Id, newEvents, cancellationToken);

        return new ChecklistSuggestionOutcome.Success(TaskTemplate.Replay([.. loaded.Events, .. newEvents]));
    }
}
