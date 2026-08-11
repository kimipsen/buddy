using System;

namespace Project.Domain.Orders.Events;

public interface IOrderEvent { DateTime OccurredAt { get; } }

public sealed record OrderCreated(Project.Domain.Orders.OrderId OrderId, DateTime OccurredAt) : IOrderEvent;
public sealed record ItemAdded(Project.Domain.Orders.OrderId OrderId, Guid ItemId, int Quantity, DateTime OccurredAt) : IOrderEvent;
public sealed record OrderCompleted(Project.Domain.Orders.OrderId OrderId, DateTime OccurredAt) : IOrderEvent;
