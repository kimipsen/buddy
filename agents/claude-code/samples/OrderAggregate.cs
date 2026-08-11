using System;
using System.Collections.Generic;
using System.Linq;
using Project.Domain.Orders.Events;

namespace Project.Domain.Orders;

public class Order
{
    private readonly List<Project.Domain.Orders.Events.OrderEvent> _changes = new();
    public OrderId Id { get; private set; }
    public bool IsCompleted { get; private set; }

        public IEnumerable<Project.Domain.Orders.Events.OrderEvent> GetChanges() => _changes.AsEnumerable();

    private void Apply(Project.Domain.Orders.Events.OrderEvent @event)
    {
        switch (@event)
        {
            case OrderCreated e:
                Id = e.OrderId;
                IsCompleted = false;
                break;
            case ItemAdded e:
                // apply item added to internal state (omitted for brevity)
                break;
            case OrderCompleted e:
                IsCompleted = true;
                break;
        }
    }

    private void Raise(Project.Domain.Orders.Events.OrderEvent @event)
    {
        Apply(@event);
        _changes.Add(@event);
    }

    public static Order Create(OrderId id)
    {
        var o = new Order();
        o.Raise(new OrderCreated(id, DateTime.UtcNow));
        return o;
    }

    public void AddItem(Guid itemId, int quantity)
    {
        if (IsCompleted) throw new InvalidOperationException("Cannot add item to completed order");
        Raise(new ItemAdded(Id, itemId, quantity, DateTime.UtcNow));
    }

    public void Complete()
    {
        if (IsCompleted) return;
        Raise(new OrderCompleted(Id, DateTime.UtcNow));
    }

        public void LoadsFrom(IEnumerable<Project.Domain.Orders.Events.OrderEvent> events)
    {
        foreach (var e in events) Apply(e);
    }
}
