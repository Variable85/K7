using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using K7.Clients.MAUI.Linux;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Single-instance detection and IPC. A lock file held open by the primary says whether an
/// instance is running (so a second launch never boots a second GTK application, which the labs
/// <c>Gtk.Application</c> would otherwise activate a second time). A Unix domain socket carries
/// the protocol URI (Windows: <c>AppInstance</c> redirect).
/// </summary>
internal static class LinuxSingleInstance
{
    private static Socket? _listener;
    private static FileStream? _primaryLock;
    private static int _listening;

    public static string SocketPath => LinuxProtocolCallback.ResolveSocketPath(
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
        Path.GetTempPath(),
        Environment.UserName);

    public static string LockPath => LinuxProtocolCallback.ResolveLockPath(
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
        Path.GetTempPath(),
        Environment.UserName);

    /// <summary>
    /// True when this process is now the primary instance. False when another instance holds
    /// the lock. On any I/O failure the process proceeds as primary (never block startup).
    /// </summary>
    public static bool TryAcquirePrimaryLock()
    {
        if (_primaryLock is not null)
            return true;

        var path = LockPath;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            _primaryLock = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            // Held by the running instance.
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - primary lock failed: " + ex.Message);
            return true;
        }
    }

    public static void ReleasePrimaryLock()
    {
        var handle = Interlocked.Exchange(ref _primaryLock, null);
        if (handle is null)
            return;

        try
        {
            handle.Dispose();
            File.Delete(LockPath);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Second launch: hand the URI (or a plain activate) to the primary instance. Returns
    /// false when no instance is listening (stale socket files are removed).
    /// </summary>
    public static bool TryForwardToPrimary(Uri? callback)
    {
        var path = SocketPath;
        if (!File.Exists(path))
            return false;

        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.Connect(new UnixDomainSocketEndPoint(path));

            var message = callback?.AbsoluteUri ?? LinuxProtocolCallback.ActivateMessage;
            client.Send(Encoding.UTF8.GetBytes(message + "\n"));
            client.Shutdown(SocketShutdown.Send);

            // Wait for the primary to acknowledge so the message is not lost on exit.
            client.ReceiveTimeout = 2000;
            try
            {
                client.Receive(new byte[1]);
            }
            catch (SocketException)
            {
            }

            return true;
        }
        catch (SocketException)
        {
            TryDelete(path);
            return false;
        }
    }

    public static void StartListening(Action<string> onMessage)
    {
        if (Interlocked.Exchange(ref _listening, 1) != 0)
            return;

        var path = SocketPath;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            TryDelete(path);

            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(4);
            _listener = listener;
            _ = Task.Run(() => AcceptLoopAsync(listener, onMessage));
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - single instance listener failed: " + ex);
        }
    }

    public static void Stop()
    {
        var listener = Interlocked.Exchange(ref _listener, null);
        listener?.Dispose();
        TryDelete(SocketPath);
        ReleasePrimaryLock();
    }

    private static async Task AcceptLoopAsync(Socket listener, Action<string> onMessage)
    {
        while (true)
        {
            Socket client;
            try
            {
                client = await listener.AcceptAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = Task.Run(() => HandleClient(client, onMessage));
        }
    }

    private static void HandleClient(Socket client, Action<string> onMessage)
    {
        using (client)
        {
            try
            {
                var buffer = new byte[4096];
                using var received = new MemoryStream();
                int read;
                while ((read = client.Receive(buffer)) > 0)
                {
                    received.Write(buffer, 0, read);
                    if (received.Length > 16 * 1024)
                        break;
                }

                var text = Encoding.UTF8.GetString(received.ToArray());
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    onMessage(line);

                try
                {
                    client.Send("\n"u8.ToArray());
                }
                catch (SocketException)
                {
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("K7 MAUI - single instance message failed: " + ex);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
