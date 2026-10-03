namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Linux MAUI (GTK4 / WebKitGTK) plays like Windows: LibVLC for muxed Direct Play and local
/// files, Video.js in the WebView for HLS transcode, audioplayer.js for music. The only
/// Linux-specific rule is that HLS keeps the Blazor chrome (no native XAML overlay over the
/// WebView). See docs/dev/video-playback.md.
/// </summary>
public static class LinuxDesktopPlayback
{
    /// <summary>
    /// True for the GTK desktop host only. Android reports IsAndroid (not IsLinux) and Web WASM
    /// reports IsBrowser, so neither is affected.
    /// </summary>
    public static bool IsLinuxDesktop =>
        OperatingSystem.IsLinux() && !OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid();
}
