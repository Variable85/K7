using System.Diagnostics;
using System.Runtime.InteropServices;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Fills in the desktop session variables that GTK, PulseAudio / PipeWire and libvlc read from
/// the C environment when the launching shell did not export them (WSLg started from a
/// non-login shell, plain <c>dotnet run</c> from a terminal multiplexer). Without
/// <c>XDG_RUNTIME_DIR</c> and <c>PULSE_SERVER</c>, WebKit media fails with "Internal data stream
/// error" and VLC falls back to a non-existent ALSA "default" device.
/// </summary>
internal static class LinuxSessionEnvironment
{
    private const string WslgRuntimeDirectory = "/mnt/wslg/runtime-dir";
    private const string WslgPulseServer = "/mnt/wslg/PulseServer";

    public static void EnsureDefaults()
    {
        // WebKit's Web Audio backend (OpenAL Soft) probes PipeWire then bare ALSA and spams
        // "cannot find card '0'" under WSLg. Point it at the Pulse server we export below.
        // No ALSA fallback: the ALSA "default" device does not exist under WSLg and every retry
        // prints a 9-line "cannot find card '0'" block that drowns the terminal.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALSOFT_DRIVERS")))
            Set("ALSOFT_DRIVERS", "pulse");

        // WebKit media runs on GStreamer autoaudiosink: make sure it lands on PulseAudio
        // (the ALSA default device does not exist under WSLg).
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GST_PLUGIN_FEATURE_RANK")))
            Set("GST_PLUGIN_FEATURE_RANK", "pulsesink:MAX,alsasink:NONE");

        try
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")))
            {
                var runtimeDir = Directory.Exists(WslgRuntimeDirectory)
                    ? WslgRuntimeDirectory
                    : "/run/user/" + getuid().ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (Directory.Exists(runtimeDir))
                    Set("XDG_RUNTIME_DIR", runtimeDir);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PULSE_SERVER"))
                && File.Exists(WslgPulseServer))
            {
                Set("PULSE_SERVER", "unix:" + WslgPulseServer);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - session environment defaults failed: " + ex.Message);
        }
    }

    /// <summary>
    /// .NET keeps its own copy of the environment: native libraries and child processes
    /// (WebKit web processes) only see <c>setenv</c>.
    /// </summary>
    /// <summary>Sets a variable for this process and the native processes it spawns.</summary>
    public static void Export(string name, string value) => Set(name, value);

    private static void Set(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _ = setenv(name, value, 1);
        Debug.WriteLine("K7 MAUI - session default " + name + "=" + value);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int setenv(string name, string value, int overwrite);

    [DllImport("libc")]
    private static extern uint getuid();
}
