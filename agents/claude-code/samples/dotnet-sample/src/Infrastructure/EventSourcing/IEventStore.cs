using System.Collections.Generic;
using System.Threading.Tasks;

namespace Project.Infrastructure.EventSourcing;

public interface IEventStore
{
    Task AppendEventsAsync(string streamName, IEnumerable<object> events, System.Guid expectedVersion = default);
    Task<IEnumerable<object>> LoadEventsAsync(string streamName);
}
