namespace buddy.Features.TransitionWarnings;

public static class GetMyTransitionWarningsHandler
{
    // Any caller reads their own settings; one no guardian has configured (and every adult) gets
    // the initial ones, which are switched off.
    public static async Task<MyTransitionWarningsResponse> Handle(
        GetMyTransitionWarnings query, ITransitionWarningSettingsEventStore settings, CancellationToken cancellationToken)
    {
        var current = await settings.FindSnapshotAsync(TransitionWarningSettingsId.ForChild(query.UserId), cancellationToken)
            ?? TransitionWarningSettings.ForChild(query.UserId);

        return MyTransitionWarningsResponse.From(current);
    }
}
