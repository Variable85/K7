using K7.Clients.MAUI.Playback;
using Microsoft.Maui.Platforms.Linux.Gtk4.Platform;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Owns the layout of the page root on GTK. The labs <c>LayoutHandler</c> arranges the root
/// once at connect time with the window default size and only again on a
/// <c>default-width/height</c> notification or when children are added: a maximized or
/// fullscreen window, or a child that becomes visible, is never re-laid out (truncated overlay,
/// video stuck top-left, hidden settings panel). This driver marks the root panel as externally
/// managed and re-runs the MAUI measure/arrange pass from a frame-clock tick whenever the panel
/// allocation changes or the tree invalidates its measure, then re-applies the ZIndex order
/// GTK ignores.
/// </summary>
internal sealed class LinuxRootLayoutDriver
{
    private readonly Layout _root;
    private GtkLayoutPanel? _panel;
    private int _lastWidth;
    private int _lastHeight;
    private bool _dirty = true;
    private bool _inLayout;

    private LinuxRootLayoutDriver(Layout root)
    {
        _root = root;
    }

    public static LinuxRootLayoutDriver Attach(Layout root)
    {
        var driver = new LinuxRootLayoutDriver(root);
        root.HandlerChanged += driver.OnHandlerChanged;
        root.MeasureInvalidated += driver.OnMeasureInvalidated;
        driver.OnHandlerChanged(null, EventArgs.Empty);
        return driver;
    }

    /// <summary>Forces a full pass on the next frame.</summary>
    public void Invalidate() => _dirty = true;

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (_root.Handler?.PlatformView is not GtkLayoutPanel panel || ReferenceEquals(panel, _panel))
            return;

        _panel = panel;
        // Labs: skips its own idle layout, resize hook and LayoutDirty tick for this panel.
        panel.IsExternallyManaged = true;
        _lastWidth = 0;
        _lastHeight = 0;
        _dirty = true;
        panel.AddTickCallback((_, _) =>
        {
            Tick();
            return true;
        });
        VlcPlayerLog.Info("gtk root layout driver attached");
    }

    private void OnMeasureInvalidated(object? sender, EventArgs e)
    {
        if (!_inLayout)
            _dirty = true;
    }

    private void Tick()
    {
        var panel = _panel;
        if (panel is null)
            return;

        var width = panel.GetWidth();
        var height = panel.GetHeight();
        if (width < 2 || height < 2)
            return;

        var sizeChanged = width != _lastWidth || height != _lastHeight;
        if (!sizeChanged && !_dirty)
            return;

        _dirty = false;
        _lastWidth = width;
        _lastHeight = height;
        _inLayout = true;
        try
        {
            if (sizeChanged)
            {
                _root.InvalidateMeasure();
                VlcPlayerLog.Info("gtk root layout " + width + "x" + height);
            }

            var view = (IView)_root;
            view.Measure(width, height);
            view.Arrange(new Rect(0, 0, width, height));
            LinuxWidgetStack.SortByZIndex(_root);
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("gtk root layout " + ex.GetType().Name + " " + ex.Message);
        }
        finally
        {
            _inLayout = false;
        }
    }
}
