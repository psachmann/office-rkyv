using Google.Protobuf.WellKnownTypes;
using Riok.Mapperly.Abstractions;

namespace OfficeRkyv.Api.Features.Archives;

[Mapper]
public static partial class ArchiveMapper
{
    // The owner is never a request field: the service passes the caller.
    public static FindArchivesByOwnerQuery ToQuery(this ListArchivesRequest request, Guid callerId) => new(callerId);

    public static CreateArchiveCommand ToCommand(this CreateArchiveRequest request, Guid callerId) =>
        new(callerId, request.Name);

    public static RenameArchiveCommand ToCommand(this RenameArchiveRequest request, Guid callerId) =>
        new(Guid.Parse(request.ArchiveId), request.Version, request.Name, callerId);

    public static DeleteArchiveCommand ToCommand(this DeleteArchiveRequest request, Guid callerId) =>
        new(Guid.Parse(request.ArchiveId), request.Version, callerId);

    // Status is not exposed: deleted archives are filtered out before they reach the API.
    [MapperIgnoreSource(nameof(Archive.Status))]
    public static partial ArchiveDto ToDto(this Archive archive);

    // Hand-written: `repeated` fields are get-only RepeatedField<T>, so items must be added, not assigned.
    public static ListArchivesResponse ToResponse(this IEnumerable<Archive> archives) =>
        new() { Archives = { archives.Select(ToDto) } };

    public static CreateArchiveResponse ToCreateResponse(this Archive archive) => new() { Archive = archive.ToDto() };

    public static RenameArchiveResponse ToRenameResponse(this Archive archive) => new() { Archive = archive.ToDto() };

    private static Timestamp ToTimestamp(DateTimeOffset value) => Timestamp.FromDateTimeOffset(value);
}
