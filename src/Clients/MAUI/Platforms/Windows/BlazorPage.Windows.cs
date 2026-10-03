#if WINDOWS
using System.Globalization;
using K7.Clients.MAUI.Playback;
using K7.Clients.MAUI.Platforms.Windows;
using K7.Clients.MAUI.Platforms.Windows.Services;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using VirtualKey = Windows.System.VirtualKey;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private bool _windowsEscapeHandlerAttached;
    private bool _windowsCloseHandlerAttached;
    private KeyEventHandler? _windowsPreviewKeyDownHandler;
    private KeyEventHandler? _windowsPreviewKeyUpHandler;
    private TypedEventHandler<object, WindowEventArgs>? _windowsClosedHandler;
    private TypedEventHandler<AppWindow, AppWindowClosingEventArgs>? _windowsClosingHandler;
    private AppWindow? _windowsAppWindow;

    partial void InitializePlayerPlatform()
    {
        DisableNativeAudioElements();
        InitializeDesktopVlcRequests();
        _playerService.EnterFullScreenRequested += OnWindowsEnterFullScreen;
        _playerService.ExitFullScreenRequested += OnWindowsExitFullScreen;
        // Attach Closing before first play so exit during Direct Play always tears down LibVLC.
        EnsureWindowsCloseHandler();
    }

    partial void DetachPlayerPlatform()
    {
        _playerService.EnterFullScreenRequested -= OnWindowsEnterFullScreen;
        _playerService.ExitFullScreenRequested -= OnWindowsExitFullScreen;
        _directTrackOverrideUrl = null;
        try
        {
            _vlcPlayer?.PrepareForAppExit();
        }
        catch
        {
            StopDesktopVlc();
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
        DetachWindowsEscapeHandler();
        DetachWindowsCloseHandler();
    }

    private partial IDesktopVlcVideoPlayer CreateDesktopVlcPlayer() => new WindowsVlcVideoPlayer(RootGrid);

    partial void OnDesktopVlcSessionStarted()
    {
        // Reset any leftover mixer attenuation from older builds. Volume is software-only (0-200).
        WindowsAppAudioVolume.TrySet(1.0);
    }

    partial void OnDesktopVlcSessionStopped()
    {
        // Reset leftover mixer gain so a later Direct session starts at unity (software gain only).
        WindowsAppAudioVolume.TrySet(1.0);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        EnsureWindowsEscapeHandler();
        // Window.Handler may be null during InitializePlayerPlatform - retry Closing attach here.
        EnsureWindowsCloseHandler();
    }

    partial void ConfigureDesktopVideoPlayerLayout()
    {
        SyncWindowsStreamAuthContext();
        DisableNativeAudioElements();

        NativePlayer.IsVisible = false;
        NativePlayer.InputTransparent = true;
        NativePlayer.IsEnabled = false;
        NativePlayer.ShouldShowPlaybackControls = false;
        NativePlayerCloseButton.IsVisible = false;

        if (_playerService.IsVisible)
        {
            try
            {
                NativePlayer.Stop();
                NativePlayer.Source = null;
            }
            catch
            {
            }

            BackgroundColor = Colors.Black;
            Padding = new Microsoft.Maui.Thickness(0);
            EnsureWindowsEscapeHandler();
            EnsureWindowsCloseHandler();
        }
        else
        {
            NativePlayer.IsEnabled = true;
            try
            {
                NativePlayer.Stop();
                NativePlayer.Source = null;
            }
            catch
            {
            }

            BackgroundColor = Color.FromRgb(13, 9, 7);
            blazorWebView.BackgroundColor = Color.FromRgb(13, 9, 7);
            TryEvaluateWebViewJs(
                "try{if(window.blankK7VideoSurfaces)blankK7VideoSurfaces();"
                + "if(window.K7&&K7.setNativePlayerActive)K7.setNativePlayerActive(false,false);}catch(e){}");
            StopDesktopVlc();
        }
    }

    private static void DisableNativeAudioElements()
    {
    }

    private void EnsureWindowsEscapeHandler()
    {
        EnsureWindowsCloseHandler();
        if (_windowsEscapeHandlerAttached)
            return;

        if (!TryGetWindowsContent(out var content))
            return;

        _windowsPreviewKeyDownHandler ??= OnWindowsPreviewKeyDown;
        _windowsPreviewKeyUpHandler ??= OnWindowsPreviewKeyUp;
        content.AddHandler(UIElement.PreviewKeyDownEvent, _windowsPreviewKeyDownHandler, handledEventsToo: true);
        content.AddHandler(UIElement.PreviewKeyUpEvent, _windowsPreviewKeyUpHandler, handledEventsToo: true);
        _windowsEscapeHandlerAttached = true;
    }

    private void EnsureWindowsCloseHandler()
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native)
            return;

        _windowsAppWindow ??= native.AppWindow;
        if (_windowsAppWindow is not null && _windowsClosingHandler is null)
        {
            _windowsClosingHandler = OnWindowsAppWindowClosing;
            _windowsAppWindow.Closing += _windowsClosingHandler;
        }

        if (_windowsClosedHandler is null)
        {
            _windowsClosedHandler = OnWindowsNativeWindowClosed;
            native.Closed += _windowsClosedHandler;
        }

        _windowsCloseHandlerAttached = _windowsClosingHandler is not null || _windowsClosedHandler is not null;
    }

    private void DetachWindowsCloseHandler()
    {
        if (!_windowsCloseHandlerAttached)
            return;

        if (_windowsAppWindow is not null && _windowsClosingHandler is not null)
            _windowsAppWindow.Closing -= _windowsClosingHandler;

        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
            && _windowsClosedHandler is not null)
        {
            native.Closed -= _windowsClosedHandler;
        }

        _windowsAppWindow = null;
        _windowsCloseHandlerAttached = false;
    }

    private void OnWindowsAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Before WinUI tears down SwapChainPanel: drop D3D callbacks (no COM Dispose) and
        // background LibVLC Stop so Present cannot race window destruction.
        try
        {
            _vlcPlayer?.PrepareForAppExit();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc prepare exit " + ex.GetType().Name);
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
    }

    private void OnWindowsNativeWindowClosed(object sender, WindowEventArgs args)
    {
        // Safety net if Closing did not run (or HLS-only session).
        try
        {
            _vlcPlayer?.PrepareForAppExit();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc closed exit " + ex.GetType().Name);
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
    }

    private void DetachWindowsEscapeHandler()
    {
        if (!_windowsEscapeHandlerAttached)
            return;

        if (TryGetWindowsContent(out var content))
        {
            if (_windowsPreviewKeyDownHandler is not null)
                content.RemoveHandler(UIElement.PreviewKeyDownEvent, _windowsPreviewKeyDownHandler);
            if (_windowsPreviewKeyUpHandler is not null)
                content.RemoveHandler(UIElement.PreviewKeyUpEvent, _windowsPreviewKeyUpHandler);
        }

        _windowsEscapeHandlerAttached = false;
    }

    private static bool TryGetWindowsContent(out UIElement content)
    {
        content = null!;
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native)
            return false;

        if (native.Content is not UIElement element)
            return false;

        content = element;
        return true;
    }

    private void OnWindowsPreviewKeyDown(object sender, KeyRoutedEventArgs e) =>
        DispatchWindowsOverlayKey(e, isKeyUp: false);

    private void OnWindowsPreviewKeyUp(object sender, KeyRoutedEventArgs e) =>
        DispatchWindowsOverlayKey(e, isKeyUp: true);

    private void DispatchWindowsOverlayKey(KeyRoutedEventArgs e, bool isKeyUp)
    {
        if (!_playerService.IsVisible)
            return;

        var key = MapWindowsVirtualKey(e.Key);
        if (key is null)
            return;

        var isRepeat = !isKeyUp && e.KeyStatus.WasKeyDown;
        if (isRepeat && key is "arrowleft" or "arrowright")
        {
            e.Handled = true;
            return;
        }

        if (TryHandleNativeVideoKey(key, isKeyUp))
        {
            e.Handled = true;
            return;
        }

        if (key == "escape" && !isKeyUp)
        {
            e.Handled = true;
            if (_playerService is Services.PlayerService playerService)
                playerService.OnBackPressed();
            else
                DispatchBackAsEscape();
        }
    }

    private static string? MapWindowsVirtualKey(VirtualKey key) =>
        key switch
        {
            VirtualKey.Escape => "escape",
            VirtualKey.Space => "space",
            VirtualKey.Enter => "enter",
            VirtualKey.Left => "arrowleft",
            VirtualKey.Right => "arrowright",
            VirtualKey.Up => "arrowup",
            VirtualKey.Down => "arrowdown",
            VirtualKey.F => "f",
            VirtualKey.M => "m",
            VirtualKey.F11 => "f",
            VirtualKey.GoBack => "goback",
            (VirtualKey)0xB3 => "mediaplaypause",
            (VirtualKey)0xB2 => "mediastop",
            _ => null
        };

    private Task OnWindowsEnterFullScreen()
    {
        WindowGeometryPersistence.SetFullscreen(true);
        _playerService.IsFullScreen = true;
        return Task.CompletedTask;
    }

    private Task OnWindowsExitFullScreen()
    {
        WindowGeometryPersistence.SetFullscreen(false);
        _playerService.IsFullScreen = false;
        return Task.CompletedTask;
    }

    internal bool TryEvaluateWebViewJs(string script)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.WebView2 webView2)
                return false;

            if (webView2.CoreWebView2 is null)
                return false;

            _ = webView2.CoreWebView2.ExecuteScriptAsync(script);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
#endif
