using Microsoft.Extensions.Logging;
using OfficeRkyv.UI.Files;

namespace OfficeRkyv.Desktop.Files;

/// <summary>
/// Saves a downloaded file where the user says, through GTK's save dialog. Where the dialog cannot be used
/// (it is the experimental backend's, and may not work on every desktop), the file goes to the user's
/// Downloads directory under a name that does not clash, and the page says where.
/// </summary>
internal sealed partial class DesktopFileSaver(ILogger logger) : IFileSaver
{
    // The schema GTK's own file dialog reads. The `desktop` dev shell puts it on XDG_DATA_DIRS (see flake.nix).
    private const string FileChooserSchema = "org.gtk.gtk4.Settings.FileChooser";

    public async Task<FileSaveResult> SaveAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellation = default
    )
    {
        var chosen = await ChooseAsync(DownloadPaths.SafeFileName(fileName));

        // Dismissed: no file, and no error. The content was never read.
        if (chosen.Dismissed)
        {
            return FileSaveResult.Cancelled;
        }

        var path = chosen.Path ?? FallbackPath(fileName);

        try
        {
            await using var output = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous
            );
            await content.CopyToAsync(output, cancellation);
        }
        catch
        {
            // Half a file with the right name is worse than none.
            File.Delete(path);

            throw;
        }

        LogSaved(logger, path);

        return new FileSaveResult(true, path);
    }

    // The save dialog. A path of null with Dismissed false means it could not be shown.
    private async Task<(string? Path, bool Dismissed)> ChooseAsync(string suggestedName)
    {
        // Asked for a schema nobody can find, GTK does not throw: GLib aborts the whole process, so this cannot
        // be left to the catch below. Looking first turns it into the fallback.
        if (!HasFileChooserSchema())
        {
            LogSchemaMissing(logger, FileChooserSchema);

            return (null, false);
        }

        try
        {
            var dialog = Gtk.FileDialog.New();
            dialog.SetInitialName(suggestedName);

            var file = await dialog.SaveAsync(null);

            return (file?.GetPath(), file is null);
        }
        catch (GLib.GException exception) when (IsDismissed(exception))
        {
            return (null, true);
        }
        catch (Exception exception)
        {
            LogDialogUnavailable(logger, exception);

            return (null, false);
        }
    }

    private static bool HasFileChooserSchema() =>
        Gio.SettingsSchemaSource.GetDefault()?.Lookup(FileChooserSchema, recursive: true) is not null;

    // GTK reports "the user closed the dialog" as an error of its own domain.
    private static bool IsDismissed(GLib.GException exception) =>
        exception.Message.Contains("Dismissed", StringComparison.OrdinalIgnoreCase);

    private static string FallbackPath(string fileName)
    {
        var directory =
            GLib.Functions.GetUserSpecialDir(GLib.UserDirectory.DirectoryDownload)
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        Directory.CreateDirectory(directory);

        return DownloadPaths.Unique(directory, fileName, File.Exists);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved a downloaded file to {Path}")]
    private static partial void LogSaved(ILogger logger, string path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The GSettings schema {Schema} is not installed, so GTK's save dialog would abort the app; saving into the Downloads directory instead"
    )]
    private static partial void LogSchemaMissing(ILogger logger, string schema);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The save dialog could not be used; saving into the Downloads directory instead"
    )]
    private static partial void LogDialogUnavailable(ILogger logger, Exception exception);
}
