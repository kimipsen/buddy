using System;
using System.Collections.Generic;

namespace Project.Domain.Orders.Events;

public interface IOrderEvent { DateTime OccurredAt { get; } }

public sealed record OrderCreated(OrderId OrderId, DateTime OccurredAt) : IOrderEvent;
public sealed record ItemAdded(OrderId OrderId, Guid ItemId, int Quantity, DateTime OccurredAt) : IOrderEvent;
public sealed record OrderCompleted(OrderId OrderId, DateTime OccurredAt) : IOrderEvent;
