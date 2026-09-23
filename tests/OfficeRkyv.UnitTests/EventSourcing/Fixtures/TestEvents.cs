using OfficeRkyv.Api.Shared;

namespace OfficeRkyv.UnitTests.EventSourcing.Fixtures;

[EventType("test.created")]
internal sealed record TestCreated(string Name);

[EventType("test.renamed")]
internal sealed record TestRenamed(string Name);
