using System;
using System.Collections.Generic;
using System.Linq;
using Project.Domain.Common;
using Project.Domain.Orders.Events;

namespace Project.Domain.Orders;

public class Order
{
    private readonly List<OrderEvent> _changes = new();
    public OrderId Id { get; private set; }
    public bool IsCompleted { get; private set; }

    public IEnumerable<OrderEvent> GetChanges() => _changes.AsEnumerable();

    // A switch *expression* over a union is checked for exhaustiveness (a switch *statement* is
    // not), so adding a case to OrderEvent fails the build right here instead of being silently
    // ignored at runtime.
    private void Apply(OrderEvent @event)
    {
        (Id, IsCompleted) = @event switch
        {
            OrderEvent.OrderCreated e => (e.OrderId, false),
            OrderEvent.ItemAdded => (Id, IsCompleted), // item lines omitted for brevity
            OrderEvent.OrderCompleted => (Id, true),
        };
    }

    private void Raise(OrderEvent @event)
    {
        Apply(@event);
        _changes.Add(@event);
    }

    public static Order Create(OrderId id)
    {
        var o = new Order();
        o.Raise(new OrderEvent.OrderCreated(id, DateTime.UtcNow));
        return o;
    }

    // Adding an item to a completed order is an expected business-rule violation, not a
    // programmer error, so it's reported as a failed Result rather than thrown.
    public Result AddItem(Guid itemId, int quantity)
    {
        if (IsCompleted) return Result.Failure("Cannot add item to completed order");
        Raise(new OrderEvent.ItemAdded(Id, itemId, quantity, DateTime.UtcNow));
        return Result.Success();
    }

    public void Complete()
    {
        if (IsCompleted) return;
        Raise(new OrderEvent.OrderCompleted(Id, DateTime.UtcNow));
    }

    public void LoadsFrom(IEnumerable<OrderEvent> events)
    {
        foreach (var e in events) Apply(e);
    }

    // Rehydration from the event store, which hands back deserialized case records.
    public void LoadsFrom(IEnumerable<object> payloads)
    {
        foreach (var p in payloads) Apply(OrderEvent.FromPayload(p));
    }
}
