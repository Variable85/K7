using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;

namespace K7.Clients.MAUI.Services;

public sealed class FileMusicSessionStore : IMusicSessionStore
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FileMusicSessionStore()
    {
        _directory = Path.Combine(FileSystem.AppDataDirectory, "music-sessions");
        Directory.CreateDirectory(_directory);
    }

    public MusicSessionSnapshotDto? Read(string scopeKey)
    {
        var path = PathFor(scopeKey);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MusicSessionSnapshotDto>(json, _jsonOptions);
    }

    public void Write(string scopeKey, MusicSessionSnapshotDto snapshot)
    {
        var path = PathFor(scopeKey);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, _jsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(string scopeKey)
    {
        var path = PathFor(scopeKey);
        if (File.Exists(path))
            File.Delete(path);

        var synced = SyncedPath(scopeKey);
        if (File.Exists(synced))
            File.Delete(synced);
    }

    public bool IsSynced(string scopeKey)
    {
        var path = SyncedPath(scopeKey);
        return File.Exists(path) && File.ReadAllText(path) == "1";
    }

    public void SetSynced(string scopeKey, bool synced)
    {
        var path = SyncedPath(scopeKey);
        if (!synced)
        {
            if (File.Exists(path))
                File.Delete(path);
            return;
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, "1");
        File.Move(temp, path, overwrite: true);
    }

    private string SyncedPath(string scopeKey) => PathFor(scopeKey) + ".synced";

    private string PathFor(string scopeKey)
    {
        var safe = string.Concat(scopeKey.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' ? ch : '_'));
        return Path.Combine(_directory, safe + ".json");
    }
}
