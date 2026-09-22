namespace OfficeRkyv.Api.Shared;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventTypeAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
