using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Project.Infrastructure.EventSourcing;

public class ReflectionEventTypeMapper : IEventTypeMapper
{
    private readonly Dictionary<string, Type> _map = new();

    public ReflectionEventTypeMapper(params Assembly[] assembliesToScan)
    {
        var assemblies = assembliesToScan?.Length > 0 ? assembliesToScan : AppDomain.CurrentDomain.GetAssemblies();
        foreach (var a in assemblies)
        {
            Type[] types;
            try { types = a.GetTypes(); } catch { continue; }
            foreach (var t in types)
            {
                if (t.Namespace == null) continue;
                if (t.Namespace.EndsWith(".Events") || t.Name.EndsWith("Event") || t.Name.EndsWith("Created") || t.Name.EndsWith("Added") || t.Name.EndsWith("Completed"))
                {
                    if (!_map.ContainsKey(t.Name)) _map[t.Name] = t;
                }
            }
        }
    }

    public Type? Resolve(string typeName)
    {
        return _map.TryGetValue(typeName, out var t) ? t : null;
    }
}
