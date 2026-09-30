using Microsoft.Extensions.FileProviders;

namespace OfficeRkyv.Desktop;

/// <summary>
/// What the shared layout asks a host for: whether this is Development (it shows the query dev tools then). A
/// MAUI host registers none, and the layout's <c>IHostEnvironment</c> is the only thing it needs from one.
/// </summary>
internal sealed class DesktopHostEnvironment : IHostEnvironment
{
    public static bool IsDevelopmentEnvironment =>
        (
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ) == Environments.Development;

    public string EnvironmentName { get; set; } =
        IsDevelopmentEnvironment ? Environments.Development : Environments.Production;

    public string ApplicationName { get; set; } = "OfficeRkyv.Desktop";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
