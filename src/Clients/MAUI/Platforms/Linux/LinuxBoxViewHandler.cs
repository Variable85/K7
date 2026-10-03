using Microsoft.Maui.Platforms.Linux.Gtk4.Handlers;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// BoxView on GTK. The labs handler paints an opaque grey placeholder whenever
/// <c>Color</c> is null, which turns the overlay scrim (a <c>LinearGradientBrush</c> background)
/// into a whitish veil over the video. This one paints <c>Color</c>, else the gradient / solid
/// <c>Background</c>, else nothing.
/// </summary>
public sealed class LinuxBoxViewHandler : GtkViewHandler<IView, Gtk.DrawingArea>
{
    public static readonly IPropertyMapper<IView, LinuxBoxViewHandler> BoxMapper =
        new PropertyMapper<IView, LinuxBoxViewHandler>(ViewMapper)
        {
            // Painted by the draw func, not by widget CSS (the base mapper would set a CSS
            // gradient under the draw func and paint the scrim twice).
            [nameof(IView.Background)] = (handler, _) => handler.PlatformView?.QueueDraw(),
            [nameof(BoxView.Color)] = (handler, _) => handler.PlatformView?.QueueDraw(),
        };

    public LinuxBoxViewHandler() : base(BoxMapper)
    {
    }

    protected override Gtk.DrawingArea CreatePlatformView()
    {
        var area = Gtk.DrawingArea.New();
        area.SetDrawFunc(Draw);
        return area;
    }

    public override void PlatformArrange(Rect rect)
    {
        // No SetContentWidth/Height: that is a GTK minimum size, and a scrim arranged once at
        // 1024px would then overflow (and warn) in every smaller layout.
        base.PlatformArrange(rect);
        PlatformView?.QueueDraw();
    }

    private void Draw(Gtk.DrawingArea area, Cairo.Context cr, int width, int height)
    {
        if (VirtualView is not BoxView box)
            return;

        if (box.Color is { } color)
        {
            Fill(cr, width, height, color);
            return;
        }

        switch (((IView)box).Background)
        {
            case LinearGradientPaint linear:
                FillLinearGradient(cr, width, height, linear);
                break;
            case SolidPaint { Color: { } solid }:
                Fill(cr, width, height, solid);
                break;
        }
    }

    private static void Fill(Cairo.Context cr, int width, int height, Color color)
    {
        cr.SetSourceRgba(color.Red, color.Green, color.Blue, color.Alpha);
        cr.Rectangle(0, 0, width, height);
        cr.Fill();
    }

    private static void FillLinearGradient(Cairo.Context cr, int width, int height, LinearGradientPaint paint)
    {
        using var gradient = new Cairo.LinearGradient(
            paint.StartPoint.X * width,
            paint.StartPoint.Y * height,
            paint.EndPoint.X * width,
            paint.EndPoint.Y * height);
        foreach (var stop in paint.GradientStops.OrderBy(s => s.Offset))
        {
            var c = stop.Color;
            gradient.AddColorStopRgba(stop.Offset, c.Red, c.Green, c.Blue, c.Alpha);
        }

        cr.SetSource(gradient);
        cr.Rectangle(0, 0, width, height);
        cr.Fill();
    }
}
