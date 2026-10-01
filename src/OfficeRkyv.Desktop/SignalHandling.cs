using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace OfficeRkyv.Desktop;

/// <summary>
/// Turns SIGTERM and SIGINT into what closing the window does: the application ends, and the API is stopped
/// after it, which removes its socket. Without this the signal ends the process at once, with the socket left
/// behind and the database not closed.
/// </summary>
internal static partial class SignalHandling
{
    public static IDisposable Register(ILogger logger) =>
        new Registrations([
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => Handle(context, logger)),
            PosixSignalRegistration.Create(PosixSignal.SIGINT, context => Handle(context, logger)),
        ]);

    private static void Handle(PosixSignalContext context, ILogger logger)
    {
        // No window yet (or any more) means there is nothing to close and nothing to wait for: let the signal
        // end the process as it would have.
        if (Application.Current is not { Windows.Count: > 0 } application)
        {
            return;
        }

        context.Cancel = true;
        LogClosingOnSignal(logger, context.Signal);

        // The callback runs on a thread of its own, and GTK may only be used from its main thread. The backend
        // ends its loop when the last window is closed; `Application.Quit()` does not (spike, question 2).
        application.Dispatcher.Dispatch(() =>
        {
            foreach (var window in application.Windows.ToList())
            {
                application.CloseWindow(window);
            }
        });
    }

    private sealed class Registrations(IReadOnlyList<PosixSignalRegistration> registrations) : IDisposable
    {
        public void Dispose()
        {
            foreach (var registration in registrations)
            {
                registration.Dispose();
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Signal}: closing the window")]
    private static partial void LogClosingOnSignal(ILogger logger, PosixSignal signal);
}
