namespace K7.Clients.Shared.Helpers;

/// <summary>
/// PointerGestureRecognizer on a parent or Button eats Android/iOS taps
/// (Clicked / TapGesture never fire). Hover is a desktop cursor concern (Windows, Linux GTK).
/// </summary>
public static class NativePointerInput
{
    public static bool SupportsHoverRecognizers =>
        ForPlatform(
            isWindows: OperatingSystem.IsWindows(),
            isAndroid: OperatingSystem.IsAndroid(),
            isIos: OperatingSystem.IsIOS(),
            isLinuxDesktop: LinuxDesktopPlayback.IsLinuxDesktop);

    public static bool ForPlatform(bool isWindows, bool isAndroid, bool isIos, bool isLinuxDesktop = false)
    {
        if (isAndroid || isIos)
            return false;

        return isWindows || isLinuxDesktop;
    }
}
