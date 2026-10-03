using Microsoft.Maui.Platforms.Linux.Gtk4.Handlers;
using ILayout = Microsoft.Maui.ILayout;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Layout handler with MAUI's <c>CascadeInputTransparent=false</c> semantics. The labs mapper
/// turns <c>InputTransparent</c> into <c>SetCanTarget(false)</c>. GTK then skips the widget and
/// its whole subtree during picking, so buttons inside an input-transparent chrome grid never
/// receive clicks. Such layouts stay targetable here. <see cref="LinuxGestureBridge"/> forwards
/// clicks that land on their empty area to the siblings underneath.
/// </summary>
public sealed class LinuxLayoutHandler : LayoutHandler
{
    public static readonly IPropertyMapper<ILayout, LinuxLayoutHandler> LinuxMapper =
        new PropertyMapper<ILayout, LinuxLayoutHandler>(Mapper)
        {
            [nameof(IView.InputTransparent)] = MapInputTransparentPassThrough,
        };

    public LinuxLayoutHandler() : base(LinuxMapper, CommandMapper)
    {
    }

    public static bool IsPassThrough(ILayout layout) =>
        layout.InputTransparent && layout is Layout { CascadeInputTransparent: false };

    private static void MapInputTransparentPassThrough(LinuxLayoutHandler handler, ILayout layout)
    {
        handler.PlatformView?.SetCanTarget(!layout.InputTransparent || IsPassThrough(layout));
    }
}
