using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Services;

public sealed class MemoryMusicSessionStore : IMusicSessionStore
{
    private readonly Dictionary<string, string> _json = [];
    private readonly HashSet<string> _synced = [];
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MusicSessionSnapshotDto? Read(string scopeKey)
    {
        if (!_json.TryGetValue(scopeKey, out var json))
            return null;

        return JsonSerializer.Deserialize<MusicSessionSnapshotDto>(json, _jsonOptions);
    }

    public void Write(string scopeKey, MusicSessionSnapshotDto snapshot) =>
        _json[scopeKey] = JsonSerializer.Serialize(snapshot, _jsonOptions);

    public void Delete(string scopeKey)
    {
        _json.Remove(scopeKey);
        _synced.Remove(scopeKey);
    }

    public bool IsSynced(string scopeKey) => _synced.Contains(scopeKey);

    public void SetSynced(string scopeKey, bool synced)
    {
        if (synced)
            _synced.Add(scopeKey);
        else
            _synced.Remove(scopeKey);
    }
}
