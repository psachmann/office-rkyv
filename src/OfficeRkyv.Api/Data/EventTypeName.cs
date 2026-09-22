using System.Collections.Concurrent;
using System.Reflection;

namespace OfficeRkyv.Api.Data;

internal static class EventTypeName
{
    private static readonly ConcurrentDictionary<Type, string> NamesByType = new();
    private static readonly ConcurrentDictionary<string, Type> TypesByName = new();

    public static string For<TPayload>()
        where TPayload : class
    {
        var type = typeof(TPayload);

        if (NamesByType.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var attribute =
            type.GetCustomAttribute<EventTypeAttribute>()
            ?? throw new InvalidOperationException(
                $"'{type.FullName}' must be annotated with [EventType(\"...\")] to be used as an event payload."
            );

        var owner = TypesByName.GetOrAdd(attribute.Name, type);
        if (owner != type)
        {
            throw new InvalidOperationException(
                $"Event type name '{attribute.Name}' is used by both '{owner.FullName}' and '{type.FullName}'. Event type names must be unique."
            );
        }

        NamesByType[type] = attribute.Name;
        return attribute.Name;
    }
}
