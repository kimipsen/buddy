using System;

namespace Project.Domain.Orders;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid id) => new(id);
    public override string ToString() => Value.ToString();
}
