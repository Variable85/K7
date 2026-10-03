using Microsoft.Maui.Platforms.Linux.Gtk4.Handlers;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// GTK4 decode surface for LibVLC: a <c>Gtk.Picture</c> that <see cref="LinuxVlcVideoPlayer"/>
/// feeds with <c>Gdk.MemoryTexture</c> frames (vmem callbacks). Aspect modes map to the
/// picture content fit, so VLC always renders at the decoded size.
/// </summary>
public sealed class LinuxVlcVideoView : View
{
    public event Action<Gtk.Picture?>? PictureChanged;

    public Gtk.Picture? Picture { get; private set; }

    internal void AttachPicture(Gtk.Picture? picture)
    {
        Picture = picture;
        PictureChanged?.Invoke(picture);
    }
}

public sealed class LinuxVlcVideoViewHandler : GtkViewHandler<LinuxVlcVideoView, Gtk.Picture>
{
    public static readonly IPropertyMapper<LinuxVlcVideoView, LinuxVlcVideoViewHandler> VideoMapper =
        new PropertyMapper<LinuxVlcVideoView, LinuxVlcVideoViewHandler>(ViewMapper);

    public LinuxVlcVideoViewHandler() : base(VideoMapper)
    {
    }

    protected override Gtk.Picture CreatePlatformView()
    {
        var picture = Gtk.Picture.New();
        picture.SetCanShrink(true);
        picture.SetContentFit(Gtk.ContentFit.Contain);
        picture.SetHexpand(true);
        picture.SetVexpand(true);
        return picture;
    }

    protected override void ConnectHandler(Gtk.Picture platformView)
    {
        base.ConnectHandler(platformView);
        VirtualView.AttachPicture(platformView);
    }

    protected override void DisconnectHandler(Gtk.Picture platformView)
    {
        VirtualView.AttachPicture(null);
        base.DisconnectHandler(platformView);
    }
}
