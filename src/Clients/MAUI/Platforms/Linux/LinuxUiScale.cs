using System.Globalization;
using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Text scale for the native video chrome on GTK. MAUI sizes are device-independent pixels:
/// WinUI and Android multiply them by the display scale, GTK under WSLg / most VMs reports a
/// scale factor of 1 whatever the monitor, so 22px subtitles and 20px glyphs on a 1440p screen
/// come out visibly smaller than on the other clients. The factor follows the monitor height
/// (1080p: 1.125, 1440p: 1.5, 2160p: 2) unless <c>K7_GTK_UI_SCALE</c> overrides it.
/// </summary>
internal static class LinuxUiScale
{
    private static double? _factor;

    public static double Factor => _factor ??= Resolve();

    public static int Px(double deviceIndependentPixels) =>
        (int)Math.Round(deviceIndependentPixels * Factor);

    private static double Resolve()
    {
        var env = Environment.GetEnvironmentVariable("K7_GTK_UI_SCALE");
        if (double.TryParse(env, NumberStyles.Float, CultureInfo.InvariantCulture, out var forced) && forced > 0.25 && forced < 4)
        {
            VlcPlayerLog.Info("gtk ui scale " + forced.ToString("0.##", CultureInfo.InvariantCulture) + " (env)");
            return forced;
        }

        try
        {
            var display = Gdk.Display.GetDefault();
            var monitors = display?.GetMonitors();
            if (monitors is not null && monitors.GetNItems() > 0 && monitors.GetObject(0) is Gdk.Monitor monitor)
            {
                monitor.GetGeometry(out var geometry);
                var scaleFactor = Math.Max(1, monitor.GetScaleFactor());
                var heightPx = geometry.Height * scaleFactor;
                if (heightPx > 0)
                {
                    // Compositor scale already applied by GTK: only compensate what it did not.
                    var factor = Math.Clamp(heightPx / 960.0 / scaleFactor, 1.0, 2.0);
                    VlcPlayerLog.Info(
                        "gtk ui scale " + factor.ToString("0.##", CultureInfo.InvariantCulture)
                        + " monitor=" + geometry.Width + "x" + geometry.Height + " gtkScale=" + scaleFactor);
                    return factor;
                }
            }
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("gtk ui scale " + ex.GetType().Name + " " + ex.Message);
        }

        return 1;
    }
}
