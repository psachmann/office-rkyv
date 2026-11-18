using System.Reactive.Linq;
using Microsoft.Extensions.Logging;

namespace OfficeRkyv.Desktop.Components;

// The component's log messages: the logging source generator does not see code written in the .razor file.
public partial class Main
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Language: {Culture}, the UI is rebuilt in place")]
    private static partial void LogLanguageChanged(ILogger logger, string culture);

    private int generation;
    private IDisposable? subscription;

    protected override void OnInitialized() =>
        // The stream replays the current settings on subscribing: that culture is the one this root already renders with.
        subscription = SettingsService
            .Settings.Select(settings => settings.Culture.Name)
            .DistinctUntilChanged()
            .Skip(1)
            .Subscribe(culture =>
            {
                LogLanguageChanged(Logger, culture);

                _ = InvokeAsync(() =>
                {
                    generation++;
                    StateHasChanged();
                });
            });

    public void Dispose() => subscription?.Dispose();
}
