using System.Reflection;

namespace OfficeRkyv.Api.Data;

/// <summary>
/// Maps event payload types to their <see cref="EventTypeAttribute"/> name and back. Built eagerly
/// from every attributed type across all currently loaded, non-dynamic assemblies (not just this
/// one, so test-only payload types in a separate test assembly resolve too) so a name is resolvable
/// during replay even if no event of that type has been appended yet in this process.
/// </summary>
internal static class EventTypeName
{
    private static readonly Lazy<Maps> Registry = new(BuildMaps);

    public static string For<TPayload>()
        where TPayload : class => For(typeof(TPayload));

    public static string For(Type payloadType)
    {
        if (Registry.Value.ByType.TryGetValue(payloadType, out var name))
        {
            return name;
        }

        throw new InvalidOperationException(
            $"'{payloadType.FullName}' must be annotated with [EventType(\"...\")] to be used as an event payload."
        );
    }

    public static Type Resolve(string eventType)
    {
        if (Registry.Value.ByName.TryGetValue(eventType, out var type))
        {
            return type;
        }

        throw new InvalidOperationException($"No event payload type is registered for event type '{eventType}'.");
    }

    private static Maps BuildMaps()
    {
        var byType = new Dictionary<Type, string>();
        var byName = new Dictionary<string, Type>();

        var attributedTypes = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(GetLoadableTypes)
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<EventTypeAttribute>()))
            .Where(pair => pair.Attribute is not null);

        foreach (var (type, attribute) in attributedTypes)
        {
            if (byName.TryGetValue(attribute!.Name, out var owner))
            {
                throw new InvalidOperationException(
                    $"Event type name '{attribute.Name}' is used by both '{owner.FullName}' and '{type.FullName}'. Event type names must be unique."
                );
            }

            byType[type] = attribute.Name;
            byName[attribute.Name] = type;
        }

        return new Maps(byType, byName);
    }

    // Some loaded assemblies (e.g. dependencies with types that don't fully resolve) can throw when
    // enumerated; skip only the types that failed to load rather than the whole assembly.
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }

    private sealed record Maps(IReadOnlyDictionary<Type, string> ByType, IReadOnlyDictionary<string, Type> ByName);
}
