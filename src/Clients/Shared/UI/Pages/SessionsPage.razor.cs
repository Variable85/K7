using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using K7.Shared.Navigation;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SessionsPage : IDisposable
{
    [Inject] private NowPlayingService NowPlaying { get; set; } = default!;
    [Inject] private IMusicSessionApi Sessions { get; set; } = default!;
    [Inject] private MusicSessionPersistenceService Persistence { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private RemotePlaybackLauncher RemotePlayback { get; set; } = default!;
    [Inject] private ISyncPlayMediaLoader MediaLoader { get; set; } = default!;
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IPlayerService Video { get; set; } = default!;
    [Inject] private IMediaService Media { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private K7HubClient Hub { get; set; } = default!;

    private bool _loading = true;
    private Guid? _selfDeviceId;
    private List<MusicSessionSummaryDto> _saved = [];
    private HashSet<string> _liveKeys = [];

    private IReadOnlyList<NowPlayingSessionDto> LiveSessions
    {
        get
        {
            var list = NowPlaying.Sessions.ToList();
            if (LocalPlayback() is { } local && list.All(s => !IsThisDevice(s.DeviceId)))
                list.Insert(0, local);

            return list;
        }
    }

    private IReadOnlyList<MusicSessionSummaryDto> VisibleSaved
    {
        get
        {
            var audioLiveIds = LiveSessions
                .Where(session => session.IsAudio)
                .Select(session => session.DeviceId)
                .Where(id => id is Guid)
                .Select(id => id!.Value)
                .ToHashSet();

            return _saved.Where(session => !audioLiveIds.Contains(session.DeviceId)).ToList();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        NowPlaying.Changed += OnNowPlayingChanged;
        Hub.MusicSessionsChanged += OnMusicSessionsChanged;
        Audio.IsVisibleChanged += OnLocalPlaybackChanged;
        Audio.CurrentTrackChanged += OnLocalTrackChanged;
        Audio.PlaybackStateChanged += OnLocalStateChanged;
        Audio.CurrentTimeChanged += OnLocalTimeChanged;
        Video.IsVisibleChanged += OnLocalPlaybackChanged;
        Video.PlaybackStateChanged += OnLocalStateChanged;
        Video.CurrentTimeChanged += OnLocalTimeChanged;
        if (Guid.TryParse(DeviceStorage.Get(PreferenceKeys.DEVICE_ID), out var selfId))
            _selfDeviceId = selfId;
        try
        {
            await NowPlaying.RefreshAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
        }

        await LoadSavedAsync();
        _liveKeys = LiveKeys();
    }

    private async Task LoadSavedAsync()
    {
        try
        {
            var all = await Sessions.GetMusicSessionsAsync();
            var self = DeviceStorage.Get(PreferenceKeys.DEVICE_ID);
            _selfDeviceId = Guid.TryParse(self, out var deviceId) ? deviceId : null;
            _saved = all
                .OrderByDescending(s => _selfDeviceId is Guid selfId && s.DeviceId == selfId)
                .ThenByDescending(s => s.UpdatedAt)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _saved = [];
        }

        _loading = false;
    }

    private void OnNowPlayingChanged() => _ = InvokeAsync(RefreshAfterNowPlayingAsync);

    private void OnMusicSessionsChanged() => _ = InvokeAsync(ReloadSavedAsync);

    private async Task ReloadSavedAsync()
    {
        await LoadSavedAsync();
        StateHasChanged();
    }

    private async Task RefreshAfterNowPlayingAsync()
    {
        var ids = LiveKeys();
        if (!_liveKeys.SetEquals(ids))
        {
            _liveKeys = ids;
            await LoadSavedAsync();
        }

        StateHasChanged();
    }

    private HashSet<string> LiveKeys() =>
        NowPlaying.Sessions
            .Where(session => session.DeviceId is Guid)
            .Select(session => $"{session.DeviceId}:{(session.IsAudio ? "a" : "v")}")
            .ToHashSet();

    private void OnLocalPlaybackChanged() => InvokeAsync(StateHasChanged);

    private void OnLocalTrackChanged(AudioQueueItem? _) => InvokeAsync(StateHasChanged);

    private void OnLocalStateChanged(PlaybackState _) => InvokeAsync(StateHasChanged);

    private void OnLocalTimeChanged(double _) => InvokeAsync(StateHasChanged);

    private NowPlayingSessionDto? LocalPlayback()
    {
        if (Video.IsVisible && Video.Source.MediaId is Guid)
            return LocalVideo();

        if (Audio.IsVisible && Audio.CurrentDisplayedTrack is not null && !Audio.IsAwaitingRestoredPlay)
            return LocalAudio();

        return null;
    }

    private NowPlayingSessionDto LocalAudio()
    {
        var track = Audio.CurrentDisplayedTrack!;
        var duration = Audio.Duration > 0 ? Audio.Duration : track.Duration ?? 0;
        return new NowPlayingSessionDto
        {
            DeviceId = _selfDeviceId,
            MediaId = track.MediaId,
            IndexedFileId = track.IndexedFileId,
            MediaTitle = track.Title,
            MediaType = nameof(MediaType.MusicTrack),
            ParentId = track.AlbumId,
            Artist = track.Artist,
            AlbumTitle = track.AlbumTitle,
            ThumbnailUrl = track.CoverUrl,
            Position = Audio.CurrentTime,
            Duration = duration,
            State = (int)Audio.PlaybackState,
            IsAudio = true,
            CanControl = false
        };
    }

    private NowPlayingSessionDto LocalVideo()
    {
        var source = Video.Source;
        return new NowPlayingSessionDto
        {
            DeviceId = _selfDeviceId,
            MediaId = source.MediaId,
            IndexedFileId = source.IndexedFileId,
            MediaTitle = source.Title,
            ThumbnailUrl = source.CoverUrl,
            Position = Video.CurrentTime,
            Duration = Video.Duration,
            State = (int)Video.PlaybackState,
            IsAudio = false,
            CanControl = false
        };
    }

    private static string? MediaHref(NowPlayingSessionDto session)
    {
        if (session.MediaId is not Guid mediaId)
            return null;

        return MediaPageUrls.BuildFromTypeName(
            session.MediaType,
            mediaId,
            serieId: session.ParentId,
            seasonNumber: session.SeasonNumber,
            episodeNumber: session.EpisodeNumber,
            albumId: session.ParentId);
    }

    private static bool CanOpenSavedMedia(MusicSessionSummaryDto session) =>
        session.AlbumId is Guid || session.MediaId is Guid;

    private async Task OpenSavedMediaAsync(MusicSessionSummaryDto session)
    {
        if (session.AlbumId is Guid albumId)
        {
            Navigation.NavigateTo(TrackHref(albumId, session.MediaId));
            return;
        }

        if (session.MediaId is not Guid mediaId)
            return;

        try
        {
            if (await Media.GetMediaAsync(mediaId) is MusicTrackDto track && track.AlbumId is Guid resolvedAlbumId)
                Navigation.NavigateTo(TrackHref(resolvedAlbumId, mediaId));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
        }
    }

    private static string TrackHref(Guid albumId, Guid? mediaId) =>
        mediaId is Guid trackId
            ? $"/music/albums/{albumId}#track-{trackId}"
            : $"/music/albums/{albumId}";

    private async Task DeleteSavedAsync(MusicSessionSummaryDto session)
    {
        if (IsThisDevice(session.DeviceId))
        {
            if (Audio.Queue.Count > 0)
                Audio.ClearQueue();
            Persistence.DiscardLocal();
        }

        try
        {
            await Sessions.DeleteMusicSessionAsync(session.DeviceId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
        }

        _saved.RemoveAll(item => item.DeviceId == session.DeviceId);
    }

    private static string? LiveContext(NowPlayingSessionDto session)
    {
        if (session.IsAudio)
            return JoinContext(session.Artist, session.AlbumTitle);

        if (string.IsNullOrWhiteSpace(session.SeriesTitle))
            return null;

        if (session.SeasonNumber is int season && session.EpisodeNumber is int episode)
            return $"{session.SeriesTitle} - S{season:D2}E{episode:D2}";

        return session.SeriesTitle;
    }

    private static string? SavedContext(MusicSessionSummaryDto session) =>
        JoinContext(session.Artist, session.AlbumTitle);

    private static string? JoinContext(string? left, string? right)
    {
        var parts = new[] { left, right }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(" - ", parts);
    }

    private bool IsThisDevice(Guid? deviceId) =>
        _selfDeviceId is Guid self && deviceId == self;

    private static string DeviceLabel(string? name, string? type) =>
        string.IsNullOrWhiteSpace(name) ? type ?? string.Empty : name;

    private static double LiveProgress(NowPlayingSessionDto session) =>
        session.Duration <= 0 ? 0 : Math.Clamp(session.Position / session.Duration * 100, 0, 100);

    private static double SavedProgress(MusicSessionSummaryDto session) =>
        session.DurationSeconds is not > 0
            ? 0
            : Math.Clamp(session.PositionSeconds / session.DurationSeconds.Value * 100, 0, 100);

    private static string FormatClock(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes}:{span.Seconds:00}";
    }

    private async Task TakeControlAsync(NowPlayingSessionDto session) =>
        await RemotePlayback.AttachToDeviceAsync(session);

    private async Task ResumeHereAsync(NowPlayingSessionDto session)
    {
        await RemotePlayback.NotifyLocalTakeoverAsync(
            session.MediaTitle,
            session.MediaId,
            session.IndexedFileId,
            session.IsAudio,
            session.ThumbnailUrl,
            session.Position,
            session.Duration);

        if (session.MediaId is not Guid mediaId)
            return;

        await MediaLoader.LoadAndPlayMediaAsync(
            mediaId,
            session.MediaTitle,
            session.ThumbnailUrl,
            session.Position > 1 ? session.Position : null,
            indexedFileId: session.IndexedFileId,
            audioTrackIndex: session.AudioTrackIndex,
            subtitleTrackIndex: session.SubtitleTrackIndex,
            playbackRate: session.PlaybackRate > 0 ? session.PlaybackRate : null);
    }

    private async Task ResumeSavedAsync(MusicSessionSummaryDto summary)
    {
        var snapshot = await Sessions.GetMusicSessionAsync(summary.DeviceId);
        if (snapshot is not { Items.Count: > 0 })
            return;

        if (!IsThisDevice(summary.DeviceId))
            await NotifySavedTakeoverAsync(snapshot, summary);

        Persistence.Adopt(snapshot, replace: true);
        Audio.Play();
    }

    private async Task NotifySavedTakeoverAsync(MusicSessionSnapshotDto snapshot, MusicSessionSummaryDto summary)
    {
        var index = snapshot.CurrentIndex;
        if (index < 0 || index >= snapshot.Items.Count)
            index = snapshot.Items.ToList().FindIndex(item => item.MediaId == snapshot.CurrentMediaId);
        if (index < 0)
            index = 0;

        var current = snapshot.Items[index];
        await RemotePlayback.NotifyLocalTakeoverAsync(
            string.IsNullOrWhiteSpace(current.Title) ? summary.Title : current.Title,
            snapshot.CurrentMediaId ?? current.MediaId,
            snapshot.CurrentIndexedFileId ?? current.IndexedFileId,
            true,
            current.CoverUrl ?? summary.CoverUrl,
            snapshot.PositionSeconds,
            current.Duration ?? summary.DurationSeconds ?? 0);
    }

    public void Dispose()
    {
        NowPlaying.Changed -= OnNowPlayingChanged;
        Hub.MusicSessionsChanged -= OnMusicSessionsChanged;
        Audio.IsVisibleChanged -= OnLocalPlaybackChanged;
        Audio.CurrentTrackChanged -= OnLocalTrackChanged;
        Audio.PlaybackStateChanged -= OnLocalStateChanged;
        Audio.CurrentTimeChanged -= OnLocalTimeChanged;
        Video.IsVisibleChanged -= OnLocalPlaybackChanged;
        Video.PlaybackStateChanged -= OnLocalStateChanged;
        Video.CurrentTimeChanged -= OnLocalTimeChanged;
    }
}
