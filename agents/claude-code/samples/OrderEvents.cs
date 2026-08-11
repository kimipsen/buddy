using System;
using System.Collections.Generic;

namespace Project.Domain.Orders.Events;

public abstract record OrderEvent(DateTime OccurredAt);

public sealed record OrderCreated(OrderId OrderId, DateTime OccurredAt) : OrderEvent(OccurredAt);
public sealed record ItemAdded(OrderId OrderId, Guid ItemId, int Quantity, DateTime OccurredAt) : OrderEvent(OccurredAt);
public sealed record OrderCompleted(OrderId OrderId, DateTime OccurredAt) : OrderEvent(OccurredAt);
