namespace K7.Clients.MAUI.Services;

public partial class VolumeService
{
    public partial bool SupportsNativeVolume => false;

    public partial double Volume => 1.0;

    public partial void SetVolume(double volume)
    {
        // Linux desktop: playback volume is applied by the WebView players (Video.js / audioplayer.js).
    }
}
