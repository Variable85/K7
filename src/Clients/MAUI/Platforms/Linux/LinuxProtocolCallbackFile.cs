using K7.Clients.MAUI.Linux;
using K7.Clients.MAUI.Services.Authentication;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Fallback IPC when the single-instance socket is not reachable (primary still booting, socket
/// file wiped). The primary consumes it at attach and through a file watcher. Only <c>k7://</c>
/// values are accepted. Same role as <c>WindowsProtocolCallbackFile</c>.
/// </summary>
internal static class LinuxProtocolCallbackFile
{
    internal static string FilePath { get; set; } = LinuxProtocolCallback.ResolveCallbackFilePath(
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
        Path.GetTempPath(),
        Environment.UserName);

    public static void Write(Uri uri)
    {
        if (!LinuxProtocolCallback.IsK7Callback(uri))
            return;

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(FilePath, uri.AbsoluteUri);
            NativeAuthTrace.Write("protocol-file", uri.GetLeftPart(UriPartial.Path));
        }
        catch (Exception ex)
        {
            NativeAuthTrace.Write("protocol-error", "file " + ex.GetType().Name);
        }
    }

    public static Uri? TryReadAndDelete()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                string raw;
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                    raw = reader.ReadToEnd().Trim();

                File.Delete(FilePath);

                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || !LinuxProtocolCallback.IsK7Callback(uri))
                    return null;

                return uri;
            }
            catch (IOException)
            {
                Thread.Sleep(20);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
