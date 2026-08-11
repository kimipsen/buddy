using System;

namespace Project.Domain.Orders;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.CreateVersion7());
    public static OrderId From(Guid id) => new(id);
    public override string ToString() => Value.ToString();
}
