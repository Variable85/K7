using System.Diagnostics;
using Microsoft.Maui.Platforms.Linux.Gtk4.Handlers;
using SkiaSharp;
using SkottieAnimation = SkiaSharp.Skottie.Animation;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Splash Lottie for GTK4. SkiaSharp.Extended <c>SKLottieView</c> has no GTK handler, so the
/// animation is decoded with Skottie into an <c>SKBitmap</c> and shown as a
/// <c>Gdk.MemoryTexture</c> on a <c>Gtk.Picture</c> (same idea as the Android DecorView overlay).
/// </summary>
public sealed class LinuxLottieView : View
{
    public event Action? FirstFrameRendered;

    /// <summary>Absolute path of the Lottie JSON file.</summary>
    public string? Source { get; set; }

    /// <summary>Render scale over the requested size (2x keeps the mark crisp on HiDPI).</summary>
    public double RenderScale { get; set; } = 2.0;

    internal void RaiseFirstFrameRendered() => FirstFrameRendered?.Invoke();
}

public sealed class LinuxLottieViewHandler : GtkViewHandler<LinuxLottieView, Gtk.Picture>
{
    public static readonly IPropertyMapper<LinuxLottieView, LinuxLottieViewHandler> LottieMapper =
        new PropertyMapper<LinuxLottieView, LinuxLottieViewHandler>(ViewMapper);

    private LinuxSkottieDriver? _driver;

    public LinuxLottieViewHandler() : base(LottieMapper)
    {
    }

    protected override Gtk.Picture CreatePlatformView()
    {
        var picture = Gtk.Picture.New();
        picture.SetCanShrink(true);
        picture.SetContentFit(Gtk.ContentFit.Contain);
        return picture;
    }

    protected override void ConnectHandler(Gtk.Picture platformView)
    {
        base.ConnectHandler(platformView);
        _driver = new LinuxSkottieDriver(platformView, VirtualView);
        _driver.Start();
    }

    protected override void DisconnectHandler(Gtk.Picture platformView)
    {
        _driver?.Dispose();
        _driver = null;
        base.DisconnectHandler(platformView);
    }
}

/// <summary>Skottie frame loop on the GLib main loop (about 30 fps).</summary>
internal sealed class LinuxSkottieDriver : IDisposable
{
    private const uint FrameIntervalMs = 33;

    private readonly Gtk.Picture _picture;
    private readonly LinuxLottieView _view;
    private readonly Stopwatch _clock = new();
    private SkottieAnimation? _animation;
    private SKBitmap? _bitmap;
    private Gdk.Texture? _texture;
    private uint _timerId;
    private bool _firstFrameRaised;
    private bool _disposed;

    public LinuxSkottieDriver(Gtk.Picture picture, LinuxLottieView view)
    {
        _picture = picture;
        _view = view;
    }

    public void Start()
    {
        _animation = LoadAnimation(_view.Source);
        if (_animation is null)
            return;

        var scale = _view.RenderScale > 0 ? _view.RenderScale : 1;
        var width = (int)Math.Max(1, Math.Round((_view.WidthRequest > 0 ? _view.WidthRequest : 220) * scale));
        var height = (int)Math.Max(1, Math.Round((_view.HeightRequest > 0 ? _view.HeightRequest : 120) * scale));
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _clock.Start();
        _timerId = GLib.Functions.TimeoutAdd(0, FrameIntervalMs, RenderFrame);
    }

    public void Dispose()
    {
        _disposed = true;
        if (_timerId != 0)
        {
            try
            {
                GLib.Functions.SourceRemove(_timerId);
            }
            catch
            {
            }

            _timerId = 0;
        }

        _texture?.Dispose();
        _texture = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _animation?.Dispose();
        _animation = null;
    }

    private bool RenderFrame()
    {
        if (_disposed || _animation is null || _bitmap is null)
            return false;

        try
        {
            var duration = _animation.Duration;
            if (duration.TotalMilliseconds > 0)
            {
                var looped = _clock.Elapsed.TotalMilliseconds % duration.TotalMilliseconds;
                _animation.Seek(looped / duration.TotalMilliseconds);
            }
            else
            {
                _animation.Seek(0);
            }

            using (var canvas = new SKCanvas(_bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                _animation.Render(canvas, new SKRect(0, 0, _bitmap.Width, _bitmap.Height));
                canvas.Flush();
            }

            using var bytes = GLib.Bytes.New(_bitmap.GetPixelSpan());
            var texture = Gdk.MemoryTexture.New(
                _bitmap.Width,
                _bitmap.Height,
                Gdk.MemoryFormat.R8g8b8a8Premultiplied,
                bytes,
                (nuint)_bitmap.RowBytes);
            var previous = _texture;
            _texture = texture;
            _picture.SetPaintable(texture);
            previous?.Dispose();

            if (!_firstFrameRaised)
            {
                _firstFrameRaised = true;
                _view.RaiseFirstFrameRendered();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - splash lottie frame failed: " + ex.Message);
            return false;
        }

        return true;
    }

    private static SkottieAnimation? LoadAnimation(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            using var data = SKData.Create(path);
            return data is null ? null : SkottieAnimation.Create(data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - splash lottie load failed: " + ex.Message);
            return null;
        }
    }
}
