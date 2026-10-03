using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// GTK theme neutralization for the native video overlay. Labs maps MAUI controls onto themed
/// GTK widgets (Adwaita / Yaru buttons with borders, shadows, 38px minimums, dark-on-light
/// labels), which does not match the Windows / Android chrome. A display-wide CSS provider
/// scoped to the <c>k7-video-overlay</c> class flattens those widgets. Per-widget CSS from the
/// MAUI mappers (background, font, padding) still wins.
/// </summary>
internal static class LinuxOverlayTheme
{
    public const string CssClass = "k7-video-overlay";

    private const string Css = """
        .k7-video-overlay button,
        .k7-video-overlay button:hover,
        .k7-video-overlay button:active,
        .k7-video-overlay button:checked,
        .k7-video-overlay button:focus {
            background-color: transparent;
            background-image: none;
            border: none;
            box-shadow: none;
            outline: none;
            min-width: 0;
            min-height: 0;
            margin: 0;
            padding: 0;
            color: #ffffff;
            text-shadow: none;
            -gtk-icon-shadow: none;
            border-radius: 8px;
        }
        .k7-video-overlay button:hover {
            background-color: rgba(255,255,255,0.14);
        }
        .k7-video-overlay button:active {
            background-color: rgba(255,255,255,0.24);
        }
        .k7-video-overlay button:focus-visible {
            outline: 2px solid rgba(255,255,255,0.9);
            outline-offset: -2px;
        }
        .k7-video-overlay button label {
            color: #ffffff;
        }
        .k7-video-overlay button.k7-glyph,
        .k7-video-overlay button.k7-glyph label {
            font-size: GLYPH_PXpx;
        }
        .k7-video-overlay label {
            color: #ffffff;
            text-shadow: none;
        }
        .k7-video-overlay scrolledwindow,
        .k7-video-overlay scrolledwindow > viewport,
        .k7-video-overlay viewport {
            background-color: transparent;
            background-image: none;
            border: none;
        }
        .k7-video-overlay scrollbar {
            background-color: transparent;
        }
        .k7-video-overlay scrollbar slider {
            background-color: rgba(255,255,255,0.35);
            min-width: 4px;
        }
        .k7-video-overlay spinner {
            color: #ffffff;
        }
        .k7-video-overlay scale trough {
            background-color: rgba(255,255,255,0.25);
            border: none;
        }
        .k7-video-overlay scale highlight {
            background-color: #ffffff;
        }
        """;

    private static bool _installed;

    /// <summary>Tags the overlay root and installs the provider once per display.</summary>
    public static void Apply(Gtk.Widget? overlayRoot)
    {
        if (overlayRoot is null)
            return;

        try
        {
            if (!overlayRoot.HasCssClass(CssClass))
                overlayRoot.AddCssClass(CssClass);

            if (_installed)
                return;

            var display = Gdk.Display.GetDefault();
            if (display is null)
                return;

            var provider = Gtk.CssProvider.New();
            provider.LoadFromString(Css.Replace("GLYPH_PX", LinuxUiScale.Px(20).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Gtk.StyleContext.AddProviderForDisplay(display, provider, Gtk.Constants.STYLE_PROVIDER_PRIORITY_APPLICATION);
            _installed = true;
            VlcPlayerLog.Info("gtk overlay theme installed");
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("gtk overlay theme " + ex.GetType().Name + " " + ex.Message);
        }
    }
}
