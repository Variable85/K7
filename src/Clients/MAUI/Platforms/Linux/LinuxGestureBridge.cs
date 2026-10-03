using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.CompilerServices;
using K7.Clients.MAUI.Playback;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// The labs GTK4 handlers never attach MAUI <c>GestureRecognizers</c> to their widgets
/// (<c>GtkGestureExtensions.AttachGestures</c> exists but is not called), so Tapped, Pointer
/// and Pan events are dead on Linux. This bridge walks a visual tree, wires GTK controllers to
/// each realized view that owns recognizers and raises the MAUI events through the same
/// internal <c>Send*</c> entry points the other platforms use.
/// <para>
/// It also emulates MAUI's <c>CascadeInputTransparent=false</c>: <see cref="LinuxLayoutHandler"/>
/// keeps such layouts targetable (GTK would otherwise skip their children), so a click or a
/// pointer move that lands on their empty area is re-dispatched here to the topmost sibling
/// underneath (settings / cast panels, gesture catchers), by picking inside that sibling.
/// </para>
/// </summary>
internal static class LinuxGestureBridge
{
    private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly ConditionalWeakTable<View, WiredView> _wired = new();

    /// <summary>Per-view wiring state: one GTK controller set per recognizer kind.</summary>
    private sealed class WiredView
    {
        public bool Observed;
        public bool Taps;
        public bool Pointers;
        public bool Pans;
    }
    private static readonly ConditionalWeakTable<Layout, object> _passThroughWired = new();
    private static readonly Dictionary<nint, View> _tapViews = new();
    private static readonly Dictionary<nint, View> _pointerViews = new();
    private static readonly HashSet<Element> _watchedRoots = new();
    private static readonly MethodInfo? _sendTapped = typeof(TapGestureRecognizer).GetMethod("SendTapped", InstanceAny);
    private static readonly MethodInfo? _sendPointerEntered = typeof(PointerGestureRecognizer).GetMethod("SendPointerEntered", InstanceAny);
    private static readonly MethodInfo? _sendPointerExited = typeof(PointerGestureRecognizer).GetMethod("SendPointerExited", InstanceAny);
    private static readonly MethodInfo? _sendPointerMoved = typeof(PointerGestureRecognizer).GetMethod("SendPointerMoved", InstanceAny);
    private static readonly MethodInfo? _sendPointerPressed = typeof(PointerGestureRecognizer).GetMethod("SendPointerPressed", InstanceAny);
    private static readonly MethodInfo? _sendPointerReleased = typeof(PointerGestureRecognizer).GetMethod("SendPointerReleased", InstanceAny);
    private static bool _reflectionLogged;
    private static View? _passThroughHover;
    // Pointer-aware views containing the pass-through pointer, deepest first (MAUI semantics:
    // entering a child does not leave its parent).
    private static List<View> _passThroughHoverChain = new();
    private static IGraphicsView? _passThroughHoverGraphics;
    private static (IGraphicsView View, View Element, Point Start)? _passThroughDrag;
    private static readonly bool _tracePointer = Environment.GetEnvironmentVariable("K7_GTK_TRACE_POINTER") == "1";
    private static readonly HashSet<View> _hovered = new(ReferenceEqualityComparer.Instance);

    /// <summary>True while the pointer is inside the view or one of its descendants.</summary>
    public static bool IsPointerOver(View view)
    {
        if (_hovered.Contains(view) || _passThroughHoverChain.Contains(view))
            return true;

        foreach (var hovered in _hovered)
        {
            if (IsAncestorOf(view, hovered))
                return true;
        }

        return _passThroughHover is { } deepest && IsAncestorOf(view, deepest);
    }

    private static bool IsAncestorOf(Element ancestor, Element element)
    {
        for (var current = element.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }

        return false;
    }

    /// <summary>Pointer-aware ancestors of <paramref name="deepest"/> (inclusive), nearest first.</summary>
    private static List<View> PointerChain(View? deepest)
    {
        var chain = new List<View>();
        for (Element? current = deepest; current is not null; current = current.Parent)
        {
            if (current is View view && view.GestureRecognizers.OfType<PointerGestureRecognizer>().Any())
                chain.Add(view);
        }

        return chain;
    }

    /// <summary>
    /// Wires every realized descendant now and keeps wiring descendants added later.
    /// Safe to call repeatedly for the same root.
    /// </summary>
    public static void Attach(Element root)
    {
        if (_watchedRoots.Add(root))
        {
            root.DescendantAdded += OnDescendantAdded;
            root.HandlerChanged += OnRootHandlerChanged;
        }

        WireTree(root);
        LogReflectionStateOnce();
    }

    private static void OnRootHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Element root)
            WireTree(root);
    }

    private static void OnDescendantAdded(object? sender, ElementEventArgs e) => WireTree(e.Element);

    private static void WireTree(Element element)
    {
        if (element is Layout layout && LinuxLayoutHandler.IsPassThrough(layout))
            WirePassThrough(layout);

        if (element is View view)
            Wire(view);

        if (element is IVisualTreeElement visual)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (child is Element childElement)
                    WireTree(childElement);
            }
        }
    }

    // ----- recognizers on a realized view -----

    private static void Wire(View view)
    {
        var state = _wired.GetValue(view, _ => new WiredView());
        if (!state.Observed && view.GestureRecognizers is INotifyCollectionChanged recognizers)
        {
            // Recognizers added after realization (volume popover hover, rows rebuilt on open).
            state.Observed = true;
            recognizers.CollectionChanged += (_, _) => Wire(view);
        }

        if (view.GestureRecognizers.Count == 0)
            return;

        if (view.Handler?.PlatformView is not Gtk.Widget widget)
        {
            // Realized later: wire on the first handler.
            view.HandlerChanged -= OnViewHandlerChanged;
            view.HandlerChanged += OnViewHandlerChanged;
            return;
        }

        view.HandlerChanged -= OnViewHandlerChanged;

        // Controllers dispatch to the recognizers present at event time, so one controller
        // set per kind is enough whatever gets added later.
        if (!state.Taps && view.GestureRecognizers.OfType<TapGestureRecognizer>().Any())
        {
            state.Taps = true;
            WireTaps(widget, view);
        }

        if (!state.Pointers && view.GestureRecognizers.OfType<PointerGestureRecognizer>().Any())
        {
            state.Pointers = true;
            WirePointers(widget, view);
        }

        if (!state.Pans && view.GestureRecognizers.OfType<PanGestureRecognizer>().Any())
        {
            state.Pans = true;
            WirePans(widget, view);
        }
    }

    private static void OnViewHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is View view)
            Wire(view);
    }

    private static void WireTaps(Gtk.Widget widget, View view)
    {
        _tapViews[widget.Handle.DangerousGetHandle()] = view;
        var click = Gtk.GestureClick.New();
        click.SetButton(1);
        click.OnReleased += (_, args) =>
        {
            // GTK counts presses on the same spot: 1 = single tap, 2 = double tap.
            var presses = Math.Max(1, (int)args.NPress);
            VlcPlayerLog.Info("gtk tap " + view.GetType().Name + " n=" + presses);
            DispatchTaps(view, new Point(args.X, args.Y), presses);
        };
        widget.AddController(click);
    }

    private static void DispatchTaps(View view, Point local, int presses)
    {
        foreach (var tap in view.GestureRecognizers.OfType<TapGestureRecognizer>())
        {
            if (tap.NumberOfTapsRequired == presses)
                SendTapped(tap, view, local);
        }
    }

    private static List<PointerGestureRecognizer> PointersOf(View view) =>
        view.GestureRecognizers.OfType<PointerGestureRecognizer>().ToList();

    private static void WirePointers(Gtk.Widget widget, View view)
    {
        _pointerViews[widget.Handle.DangerousGetHandle()] = view;
        var motion = Gtk.EventControllerMotion.New();
        // GTK4 emits enter/leave on the controller only when the pointer enters or leaves the
        // widget including its descendants, which is exactly MAUI's PointerEntered/Exited.
        var lastX = double.NaN;
        var lastY = double.NaN;
        motion.OnEnter += (_, args) =>
        {
            _hovered.Add(view);
            if (_tracePointer)
                VlcPlayerLog.Info("gtk pointer enter " + view.GetType().Name + " " + view.Frame);
            SendPointer(_sendPointerEntered, PointersOf(view), view, new Point(args.X, args.Y));
        };
        motion.OnMotion += (_, args) =>
        {
            // Synthesized motion after a widget-tree change repeats the last coordinates.
            if (args.X == lastX && args.Y == lastY)
                return;

            lastX = args.X;
            lastY = args.Y;
            SendPointer(_sendPointerMoved, PointersOf(view), view, new Point(args.X, args.Y));
        };
        motion.OnLeave += (_, _) =>
        {
            lastX = double.NaN;
            lastY = double.NaN;
            _hovered.Remove(view);
            if (_tracePointer)
                VlcPlayerLog.Info("gtk pointer leave " + view.GetType().Name);
            SendPointer(_sendPointerExited, PointersOf(view), view, null);
        };
        widget.AddController(motion);

        // Gtk.Button owns its click gesture (Pressed / Clicked / Released through the handler):
        // a second GestureClick on the same widget competes for the sequence.
        if (widget is Gtk.Button)
            return;

        var click = Gtk.GestureClick.New();
        click.SetButton(1);
        click.OnPressed += (_, args) => SendPointer(_sendPointerPressed, PointersOf(view), view, new Point(args.X, args.Y));
        click.OnReleased += (_, args) => SendPointer(_sendPointerReleased, PointersOf(view), view, new Point(args.X, args.Y));
        widget.AddController(click);
    }

    private static void WirePans(Gtk.Widget widget, View view)
    {
        var drag = Gtk.GestureDrag.New();
        var gestureId = 0;
        drag.OnDragBegin += (_, _) =>
        {
            gestureId++;
            foreach (var pan in view.GestureRecognizers.OfType<PanGestureRecognizer>())
                ((IPanGestureController)pan).SendPanStarted(view, gestureId);
        };
        drag.OnDragUpdate += (_, args) =>
        {
            foreach (var pan in view.GestureRecognizers.OfType<PanGestureRecognizer>())
                ((IPanGestureController)pan).SendPan(view, args.OffsetX, args.OffsetY, gestureId);
        };
        drag.OnDragEnd += (_, _) =>
        {
            foreach (var pan in view.GestureRecognizers.OfType<PanGestureRecognizer>())
                ((IPanGestureController)pan).SendPanCompleted(view, gestureId);
        };
        widget.AddController(drag);
    }

    // ----- pass-through layouts (InputTransparent, CascadeInputTransparent = false) -----

    private static void WirePassThrough(Layout layout)
    {
        if (layout.Handler?.PlatformView is not Gtk.Widget panel)
        {
            layout.HandlerChanged -= OnPassThroughHandlerChanged;
            layout.HandlerChanged += OnPassThroughHandlerChanged;
            return;
        }

        if (_passThroughWired.TryGetValue(layout, out _))
            return;

        _passThroughWired.Add(layout, panel);
        layout.HandlerChanged -= OnPassThroughHandlerChanged;

        var click = Gtk.GestureClick.New();
        click.SetButton(1);
        click.OnReleased += (_, args) =>
        {
            try
            {
                if (!LandsOnEmptyArea(panel, args.X, args.Y))
                    return;

                ForwardClickBelow(layout, new Point(args.X + layout.X, args.Y + layout.Y), Math.Max(1, (int)args.NPress));
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("gtk pass-through tap " + ex.GetType().Name + " " + ex.Message);
            }
        };
        panel.AddController(click);

        var motion = Gtk.EventControllerMotion.New();
        motion.OnMotion += (_, args) =>
        {
            try
            {
                if (!LandsOnEmptyArea(panel, args.X, args.Y))
                {
                    UpdatePassThroughHover(null, layout, default);
                    return;
                }

                var parentPoint = new Point(args.X + layout.X, args.Y + layout.Y);
                var hit = PickBelow(layout, parentPoint);
                UpdatePassThroughHover(hit is null ? null : FindView(_pointerViews, hit.Value.Picked, hit.Value.Sibling), layout, parentPoint);
                UpdatePassThroughGraphicsHover(hit, layout, parentPoint);
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("gtk pass-through motion " + ex.GetType().Name + " " + ex.Message);
            }
        };
        motion.OnLeave += (_, _) =>
        {
            UpdatePassThroughHover(null, layout, default);
            UpdatePassThroughGraphicsHover(null, layout, default);
        };
        panel.AddController(motion);

        // Drags (and plain presses) on the empty area reach GraphicsViews underneath (volume
        // slider, seek bar): emulate what the labs GraphicsViewHandler does on direct hits.
        var drag = Gtk.GestureDrag.New();
        drag.SetButton(1);
        drag.OnDragBegin += (_, args) =>
        {
            try
            {
                _passThroughDrag = null;
                if (!LandsOnEmptyArea(panel, args.StartX, args.StartY))
                    return;

                var parentPoint = new Point(args.StartX + layout.X, args.StartY + layout.Y);
                var hit = PickBelow(layout, parentPoint);
                if (hit is null || FindGraphicsView(hit.Value.Picked, hit.Value.Sibling) is not { } graphics)
                    return;

                var local = ToLocal(graphics.Element, layout.Parent as Layout, parentPoint);
                _passThroughDrag = (graphics.View, graphics.Element, local);
                if (_tracePointer)
                    VlcPlayerLog.Info("gtk pass-through drag begin -> " + graphics.Element.GetType().Name + " " + local);
                graphics.View.StartInteraction([new PointF((float)local.X, (float)local.Y)]);
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("gtk pass-through drag " + ex.GetType().Name + " " + ex.Message);
            }
        };
        drag.OnDragUpdate += (_, args) =>
        {
            if (_passThroughDrag is not { } current)
                return;

            var point = new PointF((float)(current.Start.X + args.OffsetX), (float)(current.Start.Y + args.OffsetY));
            current.View.DragInteraction([point]);
        };
        drag.OnDragEnd += (_, args) =>
        {
            if (_passThroughDrag is not { } current)
                return;

            _passThroughDrag = null;
            var point = new PointF((float)(current.Start.X + args.OffsetX), (float)(current.Start.Y + args.OffsetY));
            current.View.EndInteraction([point], true);
        };
        panel.AddController(drag);
    }

    /// <summary>Nearest GraphicsView on the way up from the picked widget to the sibling root.</summary>
    private static (IGraphicsView View, View Element)? FindGraphicsView(Gtk.Widget picked, View sibling)
    {
        var limit = (sibling.Handler?.PlatformView as Gtk.Widget)?.Handle.DangerousGetHandle() ?? 0;
        for (var widget = picked; widget is not null; widget = widget.GetParent())
        {
            if (ResolveView(sibling, widget.Handle.DangerousGetHandle()) is GraphicsView graphics)
                return (graphics, graphics);

            if (widget.Handle.DangerousGetHandle() == limit)
                break;
        }

        return null;
    }

    /// <summary>MAUI view whose platform widget is <paramref name="handle"/>, searched under <paramref name="root"/>.</summary>
    private static View? ResolveView(Element root, nint handle)
    {
        if (root is View view && view.Handler?.PlatformView is Gtk.Widget widget && widget.Handle.DangerousGetHandle() == handle)
            return view;

        if (root is IVisualTreeElement visual)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (child is Element element && ResolveView(element, handle) is { } found)
                    return found;
            }
        }

        return null;
    }

    private static void UpdatePassThroughGraphicsHover((Gtk.Widget Picked, View Sibling)? hit, Layout layout, Point parentPoint)
    {
        var graphics = hit is null ? null : FindGraphicsView(hit.Value.Picked, hit.Value.Sibling);
        if (!ReferenceEquals(graphics?.View, _passThroughHoverGraphics))
        {
            _passThroughHoverGraphics?.EndHoverInteraction();
            _passThroughHoverGraphics = graphics?.View;
            if (graphics is { } entered)
            {
                var local = ToLocal(entered.Element, layout.Parent as Layout, parentPoint);
                entered.View.StartHoverInteraction([new PointF((float)local.X, (float)local.Y)]);
            }
        }
        else if (graphics is { } moving)
        {
            var local = ToLocal(moving.Element, layout.Parent as Layout, parentPoint);
            moving.View.MoveHoverInteraction([new PointF((float)local.X, (float)local.Y)]);
        }
    }

    private static void OnPassThroughHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Layout layout)
            WirePassThrough(layout);
    }

    private static bool LandsOnEmptyArea(Gtk.Widget panel, double x, double y)
    {
        var picked = panel.Pick(x, y, Gtk.PickFlags.Default);
        return picked is null || picked.Handle.DangerousGetHandle() == panel.Handle.DangerousGetHandle();
    }

    /// <summary>
    /// Picks inside the siblings under <paramref name="layout"/> (same parent, lower or equal
    /// ZIndex, topmost first) at a point expressed in the parent's coordinates.
    /// </summary>
    private static (Gtk.Widget Picked, View Sibling)? PickBelow(Layout layout, Point parentPoint)
    {
        if (layout.Parent is not Layout parent)
            return null;

        var candidates = parent.Children
            .OfType<View>()
            .Where(view => !ReferenceEquals(view, layout)
                && view.IsVisible
                && !view.InputTransparent
                && view.ZIndex <= layout.ZIndex
                && view.Frame.Contains(parentPoint))
            .OrderByDescending(view => view.ZIndex)
            .ThenByDescending(view => parent.Children.IndexOf(view));

        foreach (var sibling in candidates)
        {
            if (sibling.Handler?.PlatformView is not Gtk.Widget widget || !widget.GetVisible())
                continue;

            var picked = widget.Pick(parentPoint.X - sibling.Frame.X, parentPoint.Y - sibling.Frame.Y, Gtk.PickFlags.Default);
            if (picked is not null)
                return (picked, sibling);
        }

        return null;
    }

    private static void ForwardClickBelow(Layout layout, Point parentPoint, int presses)
    {
        var hit = PickBelow(layout, parentPoint);
        if (hit is null)
        {
            VlcPlayerLog.Info("gtk pass-through tap: nothing below");
            return;
        }

        // Walk up from the deepest picked widget to the sibling root: buttons are activated,
        // tap recognizers get a synthetic Tapped.
        var limit = (hit.Value.Sibling.Handler?.PlatformView as Gtk.Widget)?.Handle.DangerousGetHandle() ?? 0;
        for (var widget = hit.Value.Picked; widget is not null; widget = widget.GetParent())
        {
            if (widget is Gtk.Button button)
            {
                VlcPlayerLog.Info("gtk pass-through tap -> button");
                button.Activate();
                return;
            }

            if (_tapViews.TryGetValue(widget.Handle.DangerousGetHandle(), out var view))
            {
                VlcPlayerLog.Info("gtk pass-through tap -> " + view.GetType().Name + " n=" + presses);
                DispatchTaps(view, ToLocal(view, layout.Parent as Layout, parentPoint), presses);
                return;
            }

            if (widget.Handle.DangerousGetHandle() == limit)
                break;
        }

        VlcPlayerLog.Info("gtk pass-through tap: no target in " + hit.Value.Sibling.GetType().Name);
    }

    private static View? FindView(Dictionary<nint, View> map, Gtk.Widget picked, View sibling)
    {
        var limit = (sibling.Handler?.PlatformView as Gtk.Widget)?.Handle.DangerousGetHandle() ?? 0;
        for (var widget = picked; widget is not null; widget = widget.GetParent())
        {
            if (map.TryGetValue(widget.Handle.DangerousGetHandle(), out var view))
                return view;

            if (widget.Handle.DangerousGetHandle() == limit)
                break;
        }

        return null;
    }

    private static void UpdatePassThroughHover(View? hover, Layout layout, Point parentPoint)
    {
        var parent = layout.Parent as Layout;
        if (!ReferenceEquals(hover, _passThroughHover))
        {
            var newChain = PointerChain(hover);
            foreach (var left in _passThroughHoverChain)
            {
                if (!newChain.Contains(left))
                    SendPointer(_sendPointerExited, left.GestureRecognizers.OfType<PointerGestureRecognizer>().ToList(), left, null);
            }

            if (_tracePointer)
                VlcPlayerLog.Info("gtk pass-through hover -> " + (hover?.GetType().Name ?? "none") + (hover is null ? "" : " " + hover.Frame));

            // Outermost first so a parent sees Entered before its child, like GTK crossings.
            for (var i = newChain.Count - 1; i >= 0; i--)
            {
                var entered = newChain[i];
                if (!_passThroughHoverChain.Contains(entered))
                    SendPointer(_sendPointerEntered, entered.GestureRecognizers.OfType<PointerGestureRecognizer>().ToList(), entered, ToLocal(entered, parent, parentPoint));
            }

            _passThroughHover = hover;
            _passThroughHoverChain = newChain;
        }

        foreach (var moved in _passThroughHoverChain)
            SendPointer(_sendPointerMoved, moved.GestureRecognizers.OfType<PointerGestureRecognizer>().ToList(), moved, ToLocal(moved, parent, parentPoint));
    }

    // ----- coordinates -----

    /// <summary>Position of <paramref name="element"/> in the coordinates of <paramref name="root"/>.</summary>
    private static Point AbsolutePosition(VisualElement element, Element? root)
    {
        double x = 0, y = 0;
        for (Element? current = element; current is VisualElement visual && !ReferenceEquals(current, root); current = current.Parent)
        {
            x += visual.Frame.X;
            y += visual.Frame.Y;
        }

        return new Point(x, y);
    }

    private static Point ToLocal(View target, Layout? parent, Point parentPoint)
    {
        var origin = AbsolutePosition(target, parent);
        return new Point(parentPoint.X - origin.X, parentPoint.Y - origin.Y);
    }

    /// <summary>
    /// MAUI <c>GetPosition(relativeTo)</c>: null means window coordinates, otherwise relative to
    /// that element. Frames are summed up the visual chain (scroll offsets ignored).
    /// </summary>
    private static Func<IElement?, Point?> PositionResolver(View view, Point? local)
    {
        return relativeTo =>
        {
            if (local is null)
                return null;

            if (ReferenceEquals(relativeTo, view))
                return local;

            var viewAbs = AbsolutePosition(view, null);
            if (relativeTo is null)
                return new Point(viewAbs.X + local.Value.X, viewAbs.Y + local.Value.Y);

            if (relativeTo is not VisualElement other)
                return local;

            var otherAbs = AbsolutePosition(other, null);
            return new Point(viewAbs.X + local.Value.X - otherAbs.X, viewAbs.Y + local.Value.Y - otherAbs.Y);
        };
    }

    // ----- MAUI internals -----

    private static void SendTapped(TapGestureRecognizer tap, View view, Point position)
    {
        if (_sendTapped is null)
            return;

        try
        {
            _sendTapped.Invoke(tap, [view, PositionResolver(view, position)]);
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("gtk tap bridge " + ex.GetType().Name + " " + (ex.InnerException?.Message ?? ex.Message));
        }
    }

    private static void SendPointer(MethodInfo? method, List<PointerGestureRecognizer> pointers, View view, Point? position)
    {
        if (method is null)
            return;

        var getPosition = PositionResolver(view, position);
        foreach (var pointer in pointers)
        {
            try
            {
                method.Invoke(pointer, [view, getPosition, null, ButtonsMask.Primary]);
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("gtk pointer bridge " + ex.GetType().Name + " " + (ex.InnerException?.Message ?? ex.Message));
            }
        }
    }

    private static void LogReflectionStateOnce()
    {
        if (_reflectionLogged)
            return;

        _reflectionLogged = true;
        if (_sendTapped is null || _sendPointerMoved is null)
        {
            VlcPlayerLog.Warn(
                "gtk gesture bridge missing MAUI internals tap="
                + (_sendTapped is not null)
                + " pointer="
                + (_sendPointerMoved is not null));
        }
    }
}
