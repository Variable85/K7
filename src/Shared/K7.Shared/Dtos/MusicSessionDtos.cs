using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record MusicSessionRadioDto
{
    public required string RadioType { get; init; }
    public required string Title { get; init; }
    public Guid[]? LibraryIds { get; init; }
    public Guid[]? LibraryGroupIds { get; init; }
    public Guid? SeedTrackId { get; init; }
    public Guid? SeedArtistId { get; init; }
    public string? Genre { get; init; }
}

public sealed record MusicSessionTrackDto
{
    public required Guid IndexedFileId { get; init; }
    public Guid? RemoteIndexedFileId { get; init; }
    public required Guid MediaId { get; init; }
    public required string Title { get; init; }
    public string? Artist { get; init; }
    public string? AlbumTitle { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverDominantColor { get; init; }
    public double? Duration { get; init; }
    public Guid? ArtistId { get; init; }
    public Guid? AlbumId { get; init; }
    public string? Genre { get; init; }
    public int? UserRating { get; init; }
    public string? LocalPath { get; init; }
    public double? LoudnessLufs { get; init; }
    public double? ReplayGainTrackGain { get; init; }
    public double? Bpm { get; init; }
    public string? MusicalKey { get; init; }
    public double? Energy { get; init; }
    public double? FadeInDuration { get; init; }
    public double? FadeOutDuration { get; init; }
}

public sealed record MusicSessionSnapshotDto
{
    public MusicSessionSourceKind SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public MusicSessionRadioDto? Radio { get; init; }
    public Guid? CurrentMediaId { get; init; }
    public Guid? CurrentIndexedFileId { get; init; }
    public int CurrentIndex { get; init; }
    public double PositionSeconds { get; init; }
    public int RepeatMode { get; init; }
    public bool Shuffle { get; init; }
    public int ShuffleSeed { get; init; }
    public IReadOnlyList<MusicSessionTrackDto> Items { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record MusicSessionSummaryDto
{
    public required Guid DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? AlbumTitle { get; init; }
    public string? CoverUrl { get; init; }
    public Guid? MediaId { get; init; }
    public Guid? AlbumId { get; init; }
    public double? DurationSeconds { get; init; }
    public double PositionSeconds { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record UpsertMusicSessionRequest
{
    public Guid DeviceId { get; init; }
    public bool PositionOnly { get; init; }
    public MusicSessionSnapshotDto Snapshot { get; init; } = new();
}
