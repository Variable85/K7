using System.Text.Json;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicSessions;

internal static class DeviceMusicSessionMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static MusicSessionSnapshotDto ToSnapshot(DeviceMusicSession session) =>
        new()
        {
            SourceKind = session.SourceKind,
            SourceId = session.SourceId,
            Radio = DeserializeRadio(session.RadioJson),
            CurrentMediaId = session.CurrentMediaId,
            CurrentIndexedFileId = session.CurrentIndexedFileId,
            CurrentIndex = session.CurrentIndex,
            PositionSeconds = session.PositionSeconds,
            RepeatMode = session.RepeatMode,
            Shuffle = session.Shuffle,
            ShuffleSeed = session.ShuffleSeed,
            Items = DeserializeItems(session.ItemsJson),
            UpdatedAt = session.UpdatedAt
        };

    public static void Apply(DeviceMusicSession session, MusicSessionSnapshotDto snapshot, DateTimeOffset updatedAt)
    {
        session.SourceKind = snapshot.SourceKind;
        session.SourceId = snapshot.SourceId;
        session.RadioJson = snapshot.Radio is null ? null : JsonSerializer.Serialize(snapshot.Radio, JsonOptions);
        session.CurrentMediaId = snapshot.CurrentMediaId;
        session.CurrentIndexedFileId = snapshot.CurrentIndexedFileId;
        session.CurrentIndex = snapshot.CurrentIndex;
        session.PositionSeconds = snapshot.PositionSeconds;
        session.RepeatMode = snapshot.RepeatMode;
        session.Shuffle = snapshot.Shuffle;
        session.ShuffleSeed = snapshot.ShuffleSeed;
        session.ItemsJson = JsonSerializer.Serialize(snapshot.Items ?? [], JsonOptions);
        session.UpdatedAt = updatedAt;
    }

    public static MusicSessionSummaryDto ToSummary(DeviceMusicSession session, string deviceName)
    {
        var items = DeserializeItems(session.ItemsJson);
        MusicSessionTrackDto? current = null;
        if (session.CurrentIndex >= 0 && session.CurrentIndex < items.Count)
            current = items[session.CurrentIndex];
        current ??= items.FirstOrDefault(i => i.MediaId == session.CurrentMediaId) ?? items.FirstOrDefault();
        return new MusicSessionSummaryDto
        {
            DeviceId = session.DeviceId,
            DeviceName = deviceName,
            Title = current?.Title,
            Artist = current?.Artist,
            AlbumTitle = current?.AlbumTitle,
            CoverUrl = current?.CoverUrl,
            MediaId = current?.MediaId,
            AlbumId = current?.AlbumId,
            DurationSeconds = current?.Duration,
            PositionSeconds = session.PositionSeconds,
            UpdatedAt = session.UpdatedAt
        };
    }

    public static MusicSessionRadioDto? DeserializeRadio(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<MusicSessionRadioDto>(json, JsonOptions);

    public static IReadOnlyList<MusicSessionTrackDto> DeserializeItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<MusicSessionTrackDto>>(json, JsonOptions) ?? [];
    }

    public static bool HasSource(MusicSessionSourceKind kind) =>
        kind is MusicSessionSourceKind.Radio
            or MusicSessionSourceKind.Playlist
            or MusicSessionSourceKind.Album
            or MusicSessionSourceKind.Artist;
}
