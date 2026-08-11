using System;

namespace Project.Domain.Orders.Events;

public abstract record OrderEvent(DateTime OccurredAt);

public sealed record OrderCreated(Project.Domain.Orders.OrderId OrderId, DateTime OccurredAt) : OrderEvent(OccurredAt);
public sealed record ItemAdded(Project.Domain.Orders.OrderId OrderId, Guid ItemId, int Quantity, DateTime OccurredAt) : OrderEvent(OccurredAt);
public sealed record OrderCompleted(Project.Domain.Orders.OrderId OrderId, DateTime OccurredAt) : OrderEvent(OccurredAt);
