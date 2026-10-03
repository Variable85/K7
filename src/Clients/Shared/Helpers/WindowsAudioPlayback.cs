namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Windows MAUI uses WebView2 HTML5 / Web Audio for music (EQ, crossfade, gapless)
/// instead of CommunityToolkit MediaElement. Linux MAUI (GTK4 / WebKitGTK) takes the same path.
/// </summary>
public static class WindowsAudioPlayback
{
    public static bool UsesWebAudioPlayer => OperatingSystem.IsWindows() || LinuxDesktopPlayback.IsLinuxDesktop;
}
