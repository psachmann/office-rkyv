using Google.Rpc;
using Grpc.Core;
using OfficeRkyv.Contracts;

namespace OfficeRkyv.Web.Components.Pages.Users;

/// <summary>
/// What a failed create means for the form, as <c>CreateUserDialog.resx</c> keys - never the server's
/// own text, which is English-only and not written for end users. The dialog localizes the keys.
/// </summary>
internal sealed record CreateUserErrors(string? Email = null, string? DisplayName = null, string? General = null)
{
    private const string CreateFailed = "CreateFailed";

    public static CreateUserErrors From(Exception exception)
    {
        // Wolverine's typed client rethrows a failed call as a .NET exception wrapping the RpcException.
        if ((exception as RpcException ?? exception.InnerException as RpcException) is not { } rpc)
        {
            return new(General: CreateFailed);
        }

        return rpc.StatusCode switch
        {
            StatusCode.AlreadyExists => new(Email: "EmailTaken"),
            StatusCode.InvalidArgument => FromViolations(rpc),
            _ => new(General: CreateFailed),
        };
    }

    private static CreateUserErrors FromViolations(RpcException rpc)
    {
        var details = rpc.GetRpcStatus()?.Details.FirstOrDefault(detail => detail.Is(BadRequest.Descriptor));

        if (details is null)
        {
            return new(General: CreateFailed);
        }

        string? email = null;
        string? displayName = null;
        string? general = null;

        // First violation per field wins; the form has room for one message under each field.
        foreach (var violation in details.Unpack<BadRequest>().FieldViolations)
        {
            switch (violation.Field)
            {
                case "email":
                    email ??= Key("Email", violation.Reason);
                    break;
                case "display_name":
                    displayName ??= Key("DisplayName", violation.Reason);
                    break;
                default:
                    // A field the form does not have: better a general message than a silent failure.
                    general ??= CreateFailed;
                    break;
            }
        }

        return new(email, displayName, general);
    }

    // (field, code) -> resource key. An unknown code degrades to a generic "invalid" text.
    private static string Key(string field, string code) =>
        code switch
        {
            ViolationCodes.Required => $"{field}Required",
            ViolationCodes.InvalidFormat when field == "Email" => "EmailInvalid",
            ViolationCodes.TooLong => $"{field}TooLong",
            _ => "FieldInvalid",
        };
}
