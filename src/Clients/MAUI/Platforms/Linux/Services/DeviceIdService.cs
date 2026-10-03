using System.Security.Cryptography;
using System.Text;
using K7.Clients.MAUI.Interfaces;

namespace K7.Clients.MAUI.Platforms.Linux.Services;

/// <summary>
/// Stable per-install id. <c>/etc/machine-id</c> is hashed with an app salt so the raw
/// system id never leaves the machine. Without it a generated id is persisted in Preferences.
/// </summary>
public class DeviceIdService : IDeviceIdService
{
    private const string PreferenceKey = "LinuxDeviceUniqueId";
    private const string Salt = "k7-linux-device";

    private readonly Lazy<string> _deviceId = new(Resolve);

    public string? GetDeviceId() => _deviceId.Value;

    private static string Resolve()
    {
        var machineId = ReadMachineId();
        if (!string.IsNullOrWhiteSpace(machineId))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Salt + ":" + machineId.Trim()));
            return Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
        }

        try
        {
            var stored = Preferences.Default.Get<string?>(PreferenceKey, null);
            if (!string.IsNullOrWhiteSpace(stored))
                return stored;

            var generated = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(PreferenceKey, generated);
            return generated;
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    private static string? ReadMachineId()
    {
        foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            catch
            {
            }
        }

        return null;
    }
}
