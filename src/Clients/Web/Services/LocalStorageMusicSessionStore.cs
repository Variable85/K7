using System.Text.Json;
using Blazored.LocalStorage;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;

namespace K7.Clients.Web.Services;

public sealed class LocalStorageMusicSessionStore(ISyncLocalStorageService localStorage) : IMusicSessionStore
{
    private const string Prefix = "musicSession.";
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MusicSessionSnapshotDto? Read(string scopeKey)
    {
        var key = Prefix + scopeKey;
        if (!localStorage.ContainKey(key))
            return null;

        var json = localStorage.GetItem<string>(key);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<MusicSessionSnapshotDto>(json, _jsonOptions);
    }

    public void Write(string scopeKey, MusicSessionSnapshotDto snapshot) =>
        localStorage.SetItem(Prefix + scopeKey, JsonSerializer.Serialize(snapshot, _jsonOptions));

    public void Delete(string scopeKey)
    {
        localStorage.RemoveItem(Prefix + scopeKey);
        localStorage.RemoveItem(SyncedKey(scopeKey));
    }

    public bool IsSynced(string scopeKey) =>
        localStorage.ContainKey(SyncedKey(scopeKey))
        && localStorage.GetItem<string>(SyncedKey(scopeKey)) == "1";

    public void SetSynced(string scopeKey, bool synced)
    {
        if (synced)
            localStorage.SetItem(SyncedKey(scopeKey), "1");
        else
            localStorage.RemoveItem(SyncedKey(scopeKey));
    }

    private static string SyncedKey(string scopeKey) => Prefix + scopeKey + ".synced";
}
