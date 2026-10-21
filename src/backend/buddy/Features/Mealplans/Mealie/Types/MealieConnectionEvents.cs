using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public union MealieConnectionEvent(
    MealieConnectionInitialized,
    MealieConnected,
    MealieDisconnected
)
{
    public static MealieConnectionEvent FromPayload(object payload) => payload switch
    {
        MealieConnectionInitialized e => e,
        MealieConnected e => e,
        MealieDisconnected e => e,
        _ => throw new ArgumentException($"Unknown Mealie connection event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        MealieConnectionInitialized => nameof(MealieConnectionInitialized),
        MealieConnected => nameof(MealieConnected),
        MealieDisconnected => nameof(MealieDisconnected),
    };
}

// Appended together with the family's first MealieConnected -- the AiCredentialsInitialized
// pattern. ChildId seeds MealieConnectionIndexDocument and isn't aggregate state.
public sealed record MealieConnectionInitialized(MealieConnectionId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Only appended after a live check against the instance (ConnectMealie), so a connection in Buddy
// is one that worked at least once. Token holds the IMealieTokenCipher ciphertext, never plaintext.
public sealed record MealieConnected(
    MealieConnectionId Id,
    string BaseUrl,
    StoredApiKey Token,
    MealieHousehold Household,
    string MealieUserName,
    string MealieVersion,
    UserId ConnectedBy,
    DateTimeOffset OccurredAt);

public sealed record MealieDisconnected(MealieConnectionId Id, UserId DisconnectedBy, DateTimeOffset OccurredAt);
