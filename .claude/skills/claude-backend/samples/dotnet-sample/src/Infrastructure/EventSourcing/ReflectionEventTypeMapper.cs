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

        foreach (var assembly in assemblies)
        {
            foreach (var type in GetLoadableTypes(assembly).Where(IsEventType))
            {
                _map.TryAdd(type.Name, type);
            }
        }
    }

    public Type? Resolve(string typeName)
    {
        return _map.TryGetValue(typeName, out var t) ? t : null;
    }

    // Some referenced assemblies throw when their types are enumerated (e.g. one of their types
    // depends on an assembly that isn't present at runtime) -- skip those rather than failing
    // mapper construction entirely.
    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch
        {
            return [];
        }
    }

    private static bool IsEventType(Type type) =>
        type.Namespace is not null &&
        (type.Namespace.EndsWith(".Events") ||
         type.Name.EndsWith("Event") ||
         type.Name.EndsWith("Created") ||
         type.Name.EndsWith("Added") ||
         type.Name.EndsWith("Completed"));
}
