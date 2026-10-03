using System.Diagnostics;
using K7.Clients.MAUI.Linux;
using K7.Clients.MAUI.Services.Authentication;
using OpenIddict.Client.SystemIntegration;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// Linux counterpart of <c>WindowsProtocolActivation</c>. A <c>k7://</c> URI reaches the
/// running app on its own command line (cold start), over the single-instance socket, or
/// through the fallback callback file. OpenIddict only learns about it through
/// <c>HandleProtocolActivationAsync</c>, which completes the waiting interactive Sign in.
/// URIs that arrive before the MAUI services exist are queued and drained at attach.
/// </summary>
internal static class LinuxProtocolActivation
{
    private static readonly object PendingGate = new();
    private static Uri? _pendingCallback;
    private static FileSystemWatcher? _callbackWatcher;
    private static int _listening;
    private static int _attached;

    public static void SetStartupCallback(Uri? callback)
    {
        if (callback is null)
            return;

        lock (PendingGate)
            _pendingCallback = callback;
    }

    /// <summary>
    /// Right after <c>MauiApp.Build()</c>: open the socket and watch the callback file so a
    /// browser redirect landing during startup is never lost. Services may not exist yet.
    /// </summary>
    public static void StartListening()
    {
        if (Interlocked.Exchange(ref _listening, 1) != 0)
            return;

        LinuxSingleInstance.StartListening(OnMessage);
        WatchCallbackFile();
        NativeAuthTrace.Write("protocol-listen");
    }

    /// <summary>Once the MAUI application and its services exist (GTK lifecycle): drain queued URIs.</summary>
    public static void Attach()
    {
        StartListening();
        if (Interlocked.Exchange(ref _attached, 1) != 0)
            return;

        TryConsumeCallbackFile();
        DrainPending();
    }

    public static void Detach()
    {
        _callbackWatcher?.Dispose();
        _callbackWatcher = null;
        LinuxSingleInstance.Stop();
    }

    public static void HandleUri(Uri? uri)
    {
        if (uri is null || !LinuxProtocolCallback.IsK7Callback(uri))
            return;

        var services = IPlatformApplication.Current?.Services;
        if (services is null)
        {
            // Too early: keep it for Attach.
            NativeAuthTrace.Write("protocol-queued", uri.GetLeftPart(UriPartial.Path));
            lock (PendingGate)
                _pendingCallback = uri;
            return;
        }

        NativeAuthTrace.Write("protocol", uri.GetLeftPart(UriPartial.Path));
        _ = HandleAsync(services, uri);
    }

    private static void DrainPending()
    {
        Uri? pending;
        lock (PendingGate)
        {
            pending = _pendingCallback;
            _pendingCallback = null;
        }

        if (pending is not null)
            HandleUri(pending);
    }

    private static void OnMessage(string message)
    {
        if (string.Equals(message, LinuxProtocolCallback.ActivateMessage, StringComparison.Ordinal))
        {
            NativeAuthTrace.Write("activated", "linux");
            PresentMainWindow();
            return;
        }

        if (!Uri.TryCreate(message, UriKind.Absolute, out var uri))
            return;

        NativeAuthTrace.Write("activated", "protocol");
        PresentMainWindow();
        HandleUri(uri);
    }

    private static void WatchCallbackFile()
    {
        try
        {
            var path = LinuxProtocolCallbackFile.FilePath;
            var directory = Path.GetDirectoryName(path);
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name))
                return;

            Directory.CreateDirectory(directory);
            var watcher = new FileSystemWatcher(directory, name)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            watcher.Created += (_, _) => TryConsumeCallbackFile();
            watcher.Changed += (_, _) => TryConsumeCallbackFile();
            watcher.EnableRaisingEvents = true;
            _callbackWatcher = watcher;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - callback file watcher failed: " + ex.Message);
        }
    }

    private static void TryConsumeCallbackFile()
    {
        var uri = LinuxProtocolCallbackFile.TryReadAndDelete();
        if (uri is null)
            return;

        NativeAuthTrace.Write("activated", "file");
        PresentMainWindow();
        HandleUri(uri);
    }

    /// <summary>Bring K7 back over the browser once the code is delivered.</summary>
    private static void PresentMainWindow()
    {
        try
        {
            GLib.Functions.IdleAdd(0, () =>
            {
                try
                {
                    var app = Application.Current;
                    if (app is null)
                        return false;

                    foreach (var window in app.Windows)
                    {
                        if (window.Handler?.PlatformView is Gtk.Window gtkWindow)
                            gtkWindow.Present();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("K7 MAUI - present window failed: " + ex);
                }

                return false;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - present window failed: " + ex);
        }
    }

    private static async Task HandleAsync(IServiceProvider services, Uri uri)
    {
        try
        {
            var service = services.GetRequiredService<OpenIddictClientSystemIntegrationService>();
            await service.HandleProtocolActivationAsync(new OpenIddictClientSystemIntegrationActivation(uri))
                .ConfigureAwait(false);
            NativeAuthTrace.Write("protocol-handled");
        }
        catch (Exception ex)
        {
            NativeAuthTrace.Write("protocol-error", ex.GetType().Name + " " + ex.Message);
            Debug.WriteLine("K7 MAUI - protocol activation failed: " + ex);
        }
    }
}
