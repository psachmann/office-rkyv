using Google.Rpc;
using Grpc.Core;
using OfficeRkyv.Contracts;

namespace OfficeRkyv.UI.Components.Pages.Users;

/// <summary>
/// What a refused workspace action means for the UI, as <c>WorkspaceErrors.resx</c> keys - chosen from the
/// status code and reason code, never from the server's own text, which is English-only and not written for
/// end users. The component localizes the keys.
/// </summary>
/// <param name="Name">Under the name field of an archive dialog.</param>
/// <param name="Title">Under the title field of a document dialog.</param>
/// <param name="File">Under a file of the upload dialog: what is wrong with its content or its name.</param>
/// <param name="Query">Under the search field.</param>
/// <param name="General">For everything that is not about one field.</param>
/// <param name="ReloadNeeded">
/// The refusal means what the UI shows is out of date (the item changed or is gone), so the affected
/// lists should be loaded again.
/// </param>
internal sealed record WorkspaceErrors(
    string? Name = null,
    string? Title = null,
    string? File = null,
    string? General = null,
    bool ReloadNeeded = false,
    string? Query = null
)
{
    private const string ActionFailed = "ActionFailed";

    public static WorkspaceErrors From(Exception exception)
    {
        // Wolverine's typed client rethrows a failed call as a .NET exception wrapping the RpcException.
        if ((exception as RpcException ?? exception.InnerException as RpcException) is not { } rpc)
        {
            return new(General: ActionFailed);
        }

        return rpc.StatusCode switch
        {
            // The version the client sent is no longer current: someone else changed the item first.
            StatusCode.Aborted => new(General: "ChangedElsewhere", ReloadNeeded: true),
            StatusCode.NotFound => new(General: "NotFound", ReloadNeeded: true),
            StatusCode.FailedPrecondition => FromInvariant(rpc),
            StatusCode.InvalidArgument => FromViolations(rpc),
            _ => new(General: ActionFailed),
        };
    }

    private static WorkspaceErrors FromInvariant(RpcException rpc)
    {
        var details = rpc.GetRpcStatus()?.Details.FirstOrDefault(detail => detail.Is(ErrorInfo.Descriptor));

        return details?.Unpack<ErrorInfo>().Reason switch
        {
            InvariantCodes.UserDeleted => new(General: "UserDeleted"),
            InvariantCodes.ArchiveDeleted => new(General: "ArchiveDeleted", ReloadNeeded: true),
            InvariantCodes.DocumentDeleted => new(General: "DocumentDeleted", ReloadNeeded: true),
            InvariantCodes.ArchiveOwnerMismatch => new(General: "ArchiveOwnerMismatch"),
            _ => new(General: ActionFailed),
        };
    }

    private static WorkspaceErrors FromViolations(RpcException rpc)
    {
        var details = rpc.GetRpcStatus()?.Details.FirstOrDefault(detail => detail.Is(BadRequest.Descriptor));

        if (details is null)
        {
            return new(General: ActionFailed);
        }

        string? name = null;
        string? title = null;
        string? file = null;
        string? general = null;
        string? query = null;

        // First violation per field wins; a dialog has room for one message under each field.
        foreach (var violation in details.Unpack<BadRequest>().FieldViolations)
        {
            switch (violation.Field)
            {
                case "name":
                    name ??= Key("Name", violation.Reason);
                    break;
                case "title":
                    title ??= Key("Title", violation.Reason);
                    break;
                case "content":
                    file ??= Key("Content", violation.Reason);
                    break;
                case "file_name":
                    file ??= Key("FileName", violation.Reason);
                    break;
                case "query":
                    query ??= Key("Query", violation.Reason);
                    break;
                default:
                    // A field the dialog does not have: better a general message than a silent failure.
                    general ??= ActionFailed;
                    break;
            }
        }

        return new(name, title, file, general, Query: query);
    }

    // (field, code) -> resource key. An unknown code degrades to a generic "invalid" text.
    private static string Key(string field, string code) =>
        code switch
        {
            ViolationCodes.Required => $"{field}Required",
            ViolationCodes.TooLong => $"{field}TooLong",
            ViolationCodes.TooLarge => $"{field}TooLarge",
            ViolationCodes.TooShort => $"{field}TooShort",
            _ => "FieldInvalid",
        };
}
