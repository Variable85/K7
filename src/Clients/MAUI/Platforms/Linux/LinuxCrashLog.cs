using System.Globalization;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Last-chance crash reporter: unhandled exceptions (including those thrown inside libvlc or
/// GLib callbacks) go to stderr and to <c>$XDG_STATE_HOME/k7/crash.log</c>
/// (default <c>~/.local/state/k7/crash.log</c>) with their stack trace.
/// </summary>
internal static class LinuxCrashLog
{
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("unhandled", e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, e) => Write("unobserved-task", e.Exception);
    }

    public static string LogPath
    {
        get
        {
            var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if (string.IsNullOrEmpty(stateHome))
            {
                stateHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
            }

            return Path.Combine(stateHome, "k7", "crash.log");
        }
    }

    private static void Write(string kind, Exception exception)
    {
        var message = "K7 CRASH " + kind + ": " + exception;
        try
        {
            Console.Error.WriteLine(message);
            Console.Error.Flush();
        }
        catch
        {
        }

        try
        {
            var path = LogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(
                path,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
        }
    }
}
