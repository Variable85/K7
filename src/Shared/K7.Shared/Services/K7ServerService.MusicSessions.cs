using System.Net;
using System.Net.Http.Json;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Shared.Services;

public partial class K7ServerService
{
    public async Task<IReadOnlyList<MusicSessionSummaryDto>> GetMusicSessionsAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<MusicSessionSummaryDto>>(
            "api/users/me/music-sessions", _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<MusicSessionSnapshotDto?> GetMusicSessionAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync($"api/users/me/music-sessions/{deviceId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MusicSessionSnapshotDto>(_serializerOptions, cancellationToken);
    }

    public async Task UpsertMusicSessionAsync(UpsertMusicSessionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/music-session", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteMusicSessionAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/users/me/music-session?deviceId={deviceId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
