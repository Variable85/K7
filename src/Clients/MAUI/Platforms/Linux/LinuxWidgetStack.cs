using System.Globalization;
using System.Text;
using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// GTK4 paints siblings in child order (last on top) and the labs layout panel never reorders
/// them: a view added later always covers the earlier ones, whatever its MAUI ZIndex (the labs
/// <c>MapZIndex</c> even inverts <c>InsertAfter(parent, null)</c>, which is "first", not
/// "last"). These helpers pin the video stack explicitly and dump it for diagnostics.
/// </summary>
internal static class LinuxWidgetStack
{
    /// <summary>Moves <paramref name="widget"/> to the bottom of its parent (painted first).</summary>
    public static bool SendToBack(Gtk.Widget? widget)
    {
        if (widget?.GetParent() is not Gtk.Widget parent)
            return false;

        if (widget.GetPrevSibling() is null)
            return true;

        widget.InsertAfter(parent, null);
        return true;
    }

    /// <summary>Moves <paramref name="widget"/> to the top of its parent (painted last).</summary>
    public static bool BringToFront(Gtk.Widget? widget)
    {
        if (widget?.GetParent() is not Gtk.Widget parent)
            return false;

        if (widget.GetNextSibling() is null)
            return true;

        widget.InsertBefore(parent, null);
        return true;
    }

    public static Gtk.Widget? PlatformWidget(VisualElement? element) =>
        element?.Handler?.PlatformView as Gtk.Widget;

    /// <summary>
    /// Re-applies the MAUI ZIndex order to every realized layout panel under
    /// <paramref name="root"/> (stable: insertion order wins between equal ZIndex values).
    /// </summary>
    public static void SortByZIndex(Element root)
    {
        if (root is Layout layout && layout.Handler?.PlatformView is Gtk.Widget panel)
            SortChildren(layout, panel);

        if (root is IVisualTreeElement visual)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (child is Element element)
                    SortByZIndex(element);
            }
        }
    }

    private static int _sortTraceBudget = 40;

    private static void SortChildren(Layout layout, Gtk.Widget panel)
    {
        var panelHandle = panel.Handle.DangerousGetHandle();
        var candidates = layout.Children
            .OfType<View>()
            .Select(view => (view, widget: PlatformWidget(view)))
            .ToList();
        var ordered = candidates
            .Where(t => t.widget?.GetParent()?.Handle.DangerousGetHandle() == panelHandle)
            .OrderBy(t => t.view.ZIndex)
            .Select(t => t.widget!)
            .ToList();
        if (candidates.Count >= 2 && ordered.Count < 2 && _sortTraceBudget-- > 0)
        {
            // Children exist but none is parented to this panel: the ZIndex order cannot apply.
            var unparented = candidates.Count(t => t.widget is null);
            var elsewhere = candidates.Count(t => t.widget is not null && t.widget.GetParent()?.Handle.DangerousGetHandle() != panelHandle);
            VlcPlayerLog.Info("gtk zsort skip " + layout.GetType().Name + " children=" + candidates.Count + " noWidget=" + unparented + " otherParent=" + elsewhere);
        }

        if (ordered.Count < 2)
            return;

        var index = 0;
        var same = true;
        for (var child = panel.GetFirstChild(); child is not null; child = child.GetNextSibling())
        {
            if (index >= ordered.Count
                || child.Handle.DangerousGetHandle() != ordered[index].Handle.DangerousGetHandle())
            {
                same = false;
                break;
            }

            index++;
        }

        if (same && index == ordered.Count)
            return;

        foreach (var widget in ordered)
            widget.InsertBefore(panel, null);

        if (_sortTraceBudget-- > 0)
            VlcPlayerLog.Info("gtk zsort " + layout.GetType().Name + " reordered=" + ordered.Count);
    }

    /// <summary>GTK child order of a layout with the MAUI type and ZIndex behind each widget.</summary>
    public static void DumpLayout(string reason, Layout? layout)
    {
        if (layout is null || PlatformWidget(layout) is not { } panel)
            return;

        var byHandle = layout.Children
            .OfType<View>()
            .Select(view => (view, widget: PlatformWidget(view)))
            .Where(t => t.widget is not null)
            .ToDictionary(t => t.widget!.Handle.DangerousGetHandle(), t => t.view);
        var sb = new StringBuilder("gtk zorder ").Append(reason).Append(' ').Append(layout.GetType().Name).Append(':');
        var index = 0;
        for (var child = panel.GetFirstChild(); child is not null; child = child.GetNextSibling())
        {
            sb.Append(" [").Append(index++).Append("] ");
            if (byHandle.TryGetValue(child.Handle.DangerousGetHandle(), out var view))
                sb.Append(view.GetType().Name).Append(" z=").Append(view.ZIndex).Append(view.IsVisible ? "" : " hidden");
            else
                sb.Append(child.GetType().Name).Append(" (no view)");
        }

        VlcPlayerLog.Info(sb.ToString());
    }

    /// <summary>
    /// Logs the direct children of <paramref name="root"/> (type, visibility, opacity,
    /// hit-testing, allocation) so a VM run tells which widget covers which.
    /// </summary>
    public static void Dump(string reason, Gtk.Widget? root)
    {
        if (root is null)
        {
            VlcPlayerLog.Info("gtk stack " + reason + ": no root widget");
            return;
        }

        var sb = new StringBuilder();
        sb.Append("gtk stack ").Append(reason).Append(" root=").Append(Describe(root));
        var index = 0;
        for (var child = root.GetFirstChild(); child is not null; child = child.GetNextSibling())
        {
            sb.Append(" | [").Append(index++).Append("] ").Append(Describe(child));
        }

        VlcPlayerLog.Info(sb.ToString());
    }

    /// <summary>Logs every GtkLabel directly under <paramref name="root"/> with its text length.</summary>
    public static void DumpLabels(string reason, Gtk.Widget? root)
    {
        if (root is null)
            return;

        for (var child = root.GetFirstChild(); child is not null; child = child.GetNextSibling())
        {
            if (child is Gtk.Label label)
            {
                VlcPlayerLog.Info(
                    "gtk label " + reason + " " + Describe(label)
                    + " text=" + (label.GetText()?.Length ?? 0).ToString(CultureInfo.InvariantCulture)
                    + " css=" + string.Join(",", label.GetCssClasses()));
            }
        }
    }

    private static string Describe(Gtk.Widget widget)
    {
        var name = widget.GetType().Name;
        var size = widget.GetAllocatedWidth().ToString(CultureInfo.InvariantCulture)
            + "x" + widget.GetAllocatedHeight().ToString(CultureInfo.InvariantCulture);
        return name
            + " vis=" + (widget.GetVisible() ? 1 : 0)
            + " mapped=" + (widget.GetMapped() ? 1 : 0)
            + " op=" + widget.GetOpacity().ToString("0.##", CultureInfo.InvariantCulture)
            + " target=" + (widget.GetCanTarget() ? 1 : 0)
            + " alloc=" + size
            + " kids=" + CountChildren(widget).ToString(CultureInfo.InvariantCulture);
    }

    private static int CountChildren(Gtk.Widget widget)
    {
        var count = 0;
        for (var child = widget.GetFirstChild(); child is not null; child = child.GetNextSibling())
            count++;
        return count;
    }
}
