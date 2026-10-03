using System.Diagnostics;
using System.Reflection;
using Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Devices;
using Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Storage;

namespace K7.Clients.MAUI.Platforms.Linux;

/// <summary>
/// The labs Essentials package only wires a few static defaults (Preferences, FilePicker, ...).
/// K7 also calls <c>FileSystem</c>, <c>DeviceInfo</c>, <c>DeviceDisplay</c>, <c>Connectivity</c>,
/// <c>Launcher</c>, <c>Browser</c> and <c>MainThread</c> statically. On the plain net10.0 TFM those
/// throw until an implementation is set, which MAUI only exposes through internal setters.
/// </summary>
internal static class LinuxEssentialsDefaults
{
    private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;

    /// <summary>Before any Preferences / FileSystem access in <c>MauiProgram</c> (no DI yet).</summary>
    public static void ApplyEarly()
    {
        TrySet(typeof(FileSystem), new LinuxFileSystem());
        TrySet(typeof(DeviceInfo), new LinuxDeviceInfo());
        TrySet(typeof(Preferences), new LinuxPreferences());
    }

    /// <summary>After <c>Build()</c>, on the GTK main thread: the DI-registered labs services.</summary>
    public static void ApplyFromServices(IServiceProvider services)
    {
        TrySet(typeof(DeviceDisplay), services.GetService<IDeviceDisplay>());
        TrySet(typeof(Connectivity), services.GetService<IConnectivity>());
        TrySet(typeof(Battery), services.GetService<IBattery>());
        TrySet(typeof(Launcher), services.GetService<ILauncher>());
        TrySet(typeof(Browser), services.GetService<IBrowser>());
        TrySet(typeof(VersionTracking), services.GetService<IVersionTracking>());
        TrySet(typeof(Share), services.GetService<IShare>());
        TrySet(typeof(SecureStorage), services.GetService<ISecureStorage>());
        InstallMainThread(services);
    }

    /// <summary>
    /// <c>MainThread</c> on non-platform TFMs routes through <c>SetCustomImplementation</c>
    /// (internal, meant for custom backends). Skipped when the running MAUI build lacks it.
    /// </summary>
    private static void InstallMainThread(IServiceProvider services)
    {
        try
        {
            var dispatcher = services.GetService<IDispatcher>() ?? Application.Current?.Dispatcher;
            if (dispatcher is null)
                return;

            var method = typeof(MainThread).GetMethod("SetCustomImplementation", StaticNonPublic);
            if (method is null)
                return;

            Func<bool> isMainThread = () => !dispatcher.IsDispatchRequired;
            Action<Action> beginInvoke = action => dispatcher.Dispatch(action);
            method.Invoke(null, [isMainThread, beginInvoke]);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - MainThread install failed: " + ex);
        }
    }

    private static void TrySet(Type essentialsType, object? implementation)
    {
        if (implementation is null)
            return;

        try
        {
            var setDefault = essentialsType.GetMethod("SetDefault", StaticNonPublic);
            if (setDefault is not null && setDefault.GetParameters().Length == 1)
            {
                setDefault.Invoke(null, [implementation]);
                return;
            }

            var field = essentialsType.GetField("currentImplementation", StaticNonPublic)
                ?? essentialsType.GetField("defaultImplementation", StaticNonPublic);
            field?.SetValue(null, implementation);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - essentials default failed for " + essentialsType.Name + ": " + ex.Message);
        }
    }
}
