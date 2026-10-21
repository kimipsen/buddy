using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// A family-wide link to one Mealie household, resolved by
// MealFamilyResolution.ResolveFamilyMealieConnectionIdAsync -- the AiProviderCredential shape (see
// docs/backend/analysis/mealie-integration.md, Question 2).
public sealed record MealieConnection(
    MealieConnectionId Id,
    // Null while the family is disconnected: never connected yet, or disconnected since.
    MealieConnectionDetails? Current)
{
    public static MealieConnection? Rehydrate(IEnumerable<MealieConnectionEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static MealieConnection Replay(IEnumerable<MealieConnectionEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Not named Apply/Create: see Meal.Start.
    public static MealieConnection Start(MealieConnectionEvent @event) => @event switch
    {
        MealieConnectionInitialized created => new MealieConnection(created.Id, null),
        _ => throw EventReplay.NotAStartEvent(nameof(MealieConnection), @event.EventType)
    };

    public static MealieConnection Advance(MealieConnection connection, MealieConnectionEvent @event) => @event switch
    {
        // A reconnect (new address or rotated token) is a full overwrite.
        MealieConnected connected => connection with
        {
            Current = new MealieConnectionDetails(
                connected.BaseUrl,
                connected.Token,
                connected.Household,
                connected.MealieUserName,
                connected.MealieVersion,
                connected.ConnectedBy,
                connected.OccurredAt)
        },
        MealieDisconnected => connection with { Current = null },
        MealieConnectionInitialized => throw EventReplay.AlreadyStarted(nameof(MealieConnection), @event.EventType)
    };
}

public sealed record MealieConnectionDetails(
    string BaseUrl,
    StoredApiKey Token,
    MealieHousehold Household,
    string MealieUserName,
    string MealieVersion,
    UserId ConnectedBy,
    DateTimeOffset ConnectedAt);
