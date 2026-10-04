using System;
using Project.Domain.Orders;

namespace Project.Domain.Orders.Events;

// Requires C# unions: <TargetFramework>net11.0</TargetFramework> and <LangVersion>preview</LangVersion>.
//
// A native union is a closed set of case types: no code outside this declaration can add a case,
// and `switch` expressions over it are checked for exhaustiveness with no discard arm needed.
// Adding a case below reports CS8509 at every switch that has not handled it — the csproj
// promotes that warning to an error, so it fails the build rather than falling through at
// runtime. An `abstract record` base cannot give you this, since any assembly can derive from it.
public union OrderEvent(OrderEvent.OrderCreated, OrderEvent.ItemAdded, OrderEvent.OrderCompleted)
{
    public sealed record OrderCreated(OrderId OrderId, DateTime OccurredAt);
    public sealed record ItemAdded(OrderId OrderId, Guid ItemId, int Quantity, DateTime OccurredAt);
    public sealed record OrderCompleted(OrderId OrderId, DateTime OccurredAt);

    // Common data lives in an exhaustive projection rather than a shared base record.
    public DateTime OccurredAt => this switch
    {
        OrderCreated e => e.OccurredAt,
        ItemAdded e => e.OccurredAt,
        OrderCompleted e => e.OccurredAt,
    };

    // Persistence discriminator. A union is a value type, so `GetType().Name` on a boxed
    // OrderEvent returns "OrderEvent" for every case — use this instead when writing the
    // event_type column.
    public string EventType => this switch
    {
        OrderCreated => nameof(OrderCreated),
        ItemAdded => nameof(ItemAdded),
        OrderCompleted => nameof(OrderCompleted),
    };

    // The case record itself, for JSON serialization. Serializing the union writes the active
    // case's payload with no tag, so hand the store the payload and the EventType separately.
    public object Payload => this switch
    {
        OrderCreated e => e,
        ItemAdded e => e,
        OrderCompleted e => e,
    };

    // Rehydration: case records convert implicitly back to the union.
    public static OrderEvent FromPayload(object payload) => payload switch
    {
        OrderCreated e => e,
        ItemAdded e => e,
        OrderCompleted e => e,
        _ => throw new ArgumentException($"Unknown order event payload: {payload.GetType().Name}", nameof(payload)),
    };
}
