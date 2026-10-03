#if WINDOWS || LINUX
using K7.Clients.Shared.Enums;

namespace K7.Clients.MAUI.Playback;

/// <summary>
/// LibVLC 4 Direct Play surface shared by the desktop hosts. Windows renders through
/// D3D11 callbacks (<c>WindowsVlcVideoPlayer</c>), Linux through vmem callbacks into a
/// GTK4 <c>Gtk.Picture</c> (<c>LinuxVlcVideoPlayer</c>). <c>BlazorPage.DesktopVlc.cs</c> drives both.
/// </summary>
internal interface IDesktopVlcVideoPlayer : IDisposable
{
    event Action? Playing;
    event Action? Paused;
    event Action? Ended;
    event Action<string>? EncounteredError;
    event Action<double>? PositionChanged;
    event Action<double>? DurationChanged;
    event Action? FirstFrame;
    event Action? Reopening;

    bool IsActive { get; }

    double PositionSeconds { get; }

    double DurationSeconds { get; }

    void Play(
        string url,
        string? authorizationHeader,
        double startSeconds,
        int? audioOrdinal = null,
        int? subtitleOrdinal = null,
        int? hlsAudioTrackIndex = null,
        double knownDuration = 0);

    void UpdateAuthorization(string? authorizationHeader);

    void Resume();

    void Pause();

    void SetSurfaceVisible(bool visible);

    void Stop();

    /// <summary>App exit: release native callbacks first, then stop LibVLC off the UI thread.</summary>
    void PrepareForAppExit();

    void Seek(double seconds);

    void PinDuration(double seconds);

    void SetVolume(double volume01);

    void SetMuted(bool muted);

    void SetRate(double rate);

    void ApplyAspect(AspectRatioMode mode);

    bool TrySelectAudio(int ordinal, string? language, string? name);

    bool TrySelectSubtitle(int? ordinal, string? language, string? name);

    void SetOverlayOwnsTextSubs(bool owns);

    void RefreshSubtitleStyle();

    void LogEsTracks();
}
#endif
