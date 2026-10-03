using K7.Clients.Shared.Models;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Helpers;

public static class MusicSessionMapper
{
    public static AudioQueueItem ToQueueItem(MusicSessionTrackDto track) =>
        new()
        {
            IndexedFileId = track.IndexedFileId,
            RemoteIndexedFileId = track.RemoteIndexedFileId,
            MediaId = track.MediaId,
            Title = track.Title,
            Artist = track.Artist,
            AlbumTitle = track.AlbumTitle,
            CoverUrl = track.CoverUrl,
            CoverDominantColor = track.CoverDominantColor,
            Duration = track.Duration,
            ArtistId = track.ArtistId,
            AlbumId = track.AlbumId,
            Genre = track.Genre,
            UserRating = track.UserRating,
            LocalPath = track.LocalPath,
            LoudnessLufs = track.LoudnessLufs,
            ReplayGainTrackGain = track.ReplayGainTrackGain,
            Bpm = track.Bpm,
            MusicalKey = track.MusicalKey,
            Energy = track.Energy,
            FadeInDuration = track.FadeInDuration,
            FadeOutDuration = track.FadeOutDuration
        };

    public static MusicSessionTrackDto ToSnapshotTrack(AudioQueueItem track) =>
        new()
        {
            IndexedFileId = track.IndexedFileId,
            RemoteIndexedFileId = track.RemoteIndexedFileId,
            MediaId = track.MediaId,
            Title = track.Title,
            Artist = track.Artist,
            AlbumTitle = track.AlbumTitle,
            CoverUrl = track.CoverUrl,
            CoverDominantColor = track.CoverDominantColor,
            Duration = track.Duration,
            ArtistId = track.ArtistId,
            AlbumId = track.AlbumId,
            Genre = track.Genre,
            UserRating = track.UserRating,
            LocalPath = track.LocalPath,
            LoudnessLufs = track.LoudnessLufs,
            ReplayGainTrackGain = track.ReplayGainTrackGain,
            Bpm = track.Bpm,
            MusicalKey = track.MusicalKey,
            Energy = track.Energy,
            FadeInDuration = track.FadeInDuration,
            FadeOutDuration = track.FadeOutDuration
        };

    public static MusicSessionRadioDto ToRadioDto(MusicRadioRequest request) =>
        new()
        {
            RadioType = request.RadioType,
            Title = request.Title,
            LibraryIds = request.LibraryIds,
            LibraryGroupIds = request.LibraryGroupIds,
            SeedTrackId = request.SeedTrackId,
            SeedArtistId = request.SeedArtistId,
            Genre = request.Genre
        };

    public static MusicRadioRequest ToRadioRequest(MusicSessionRadioDto radio) =>
        new()
        {
            RadioType = radio.RadioType,
            Title = radio.Title,
            LibraryIds = radio.LibraryIds,
            LibraryGroupIds = radio.LibraryGroupIds,
            SeedTrackId = radio.SeedTrackId,
            SeedArtistId = radio.SeedArtistId,
            Genre = radio.Genre
        };
}
