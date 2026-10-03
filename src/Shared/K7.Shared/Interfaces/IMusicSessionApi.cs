using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IMusicSessionApi
{
    Task<IReadOnlyList<MusicSessionSummaryDto>> GetMusicSessionsAsync(CancellationToken cancellationToken = default);
    Task<MusicSessionSnapshotDto?> GetMusicSessionAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task UpsertMusicSessionAsync(UpsertMusicSessionRequest request, CancellationToken cancellationToken = default);
    Task DeleteMusicSessionAsync(Guid deviceId, CancellationToken cancellationToken = default);
}
