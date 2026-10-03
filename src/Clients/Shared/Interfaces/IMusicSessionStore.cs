using K7.Shared.Dtos;

namespace K7.Clients.Shared.Interfaces;

public interface IMusicSessionStore
{
    MusicSessionSnapshotDto? Read(string scopeKey);
    void Write(string scopeKey, MusicSessionSnapshotDto snapshot);
    void Delete(string scopeKey);

    /// <summary>
    /// True after this snapshot was stored on the server.
    /// False when it exists only on the device, for example while offline.
    /// </summary>
    bool IsSynced(string scopeKey);

    void SetSynced(string scopeKey, bool synced);
}
