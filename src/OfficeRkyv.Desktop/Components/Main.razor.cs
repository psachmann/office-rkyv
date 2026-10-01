using Microsoft.Extensions.Logging;

namespace OfficeRkyv.Desktop.Components;

// The component's log messages: the logging source generator does not see code written in the .razor file.
public partial class Main
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Language: {Culture}, the UI is rebuilt in place")]
    private static partial void LogLanguageChanged(ILogger logger, string culture);
}
