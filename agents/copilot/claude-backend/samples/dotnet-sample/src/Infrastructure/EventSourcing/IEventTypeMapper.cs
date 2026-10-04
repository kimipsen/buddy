using System;

namespace Project.Infrastructure.EventSourcing;

public interface IEventTypeMapper
{
    Type? Resolve(string typeName);
}
