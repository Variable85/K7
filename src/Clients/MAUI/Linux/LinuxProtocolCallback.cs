using System.Text;

namespace K7.Clients.MAUI.Linux;

/// <summary>
/// Platform-agnostic pieces of the Linux <c>k7://</c> protocol handling: desktop entry text,
/// mimeapps.list rewriting, argv parsing and the single-instance paths. Lives outside
/// <c>Platforms/Linux</c> (no GTK dependency) so the Windows smoke tests cover it.
/// </summary>
public static class LinuxProtocolCallback
{
    public const string Scheme = "k7";
    public const string ApplicationId = "com.k7.maui";
    public const string DesktopFileName = ApplicationId + ".desktop";
    public const string MimeType = "x-scheme-handler/" + Scheme;

    /// <summary>Freedesktop icon name (installed under the user hicolor theme).</summary>
    public const string IconName = ApplicationId;

    /// <summary>Message a second instance sends when launched without a URI (plain re-launch).</summary>
    public const string ActivateMessage = "activate";

    internal const string SocketFileName = "k7-maui-protocol.sock";
    internal const string LockFileName = "k7-maui-primary.lock";
    internal const string CallbackFileName = "k7-maui-protocol-callback.uri";

    public static bool IsK7Callback(Uri uri) =>
        string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase);

    public static Uri? TryGetCommandLineCallback(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (Uri.TryCreate(argument, UriKind.Absolute, out var uri) && IsK7Callback(uri))
                return uri;
        }

        return null;
    }

    /// <summary>
    /// <c>Gtk.Application</c> runs with <c>DefaultFlags</c> (no <c>HandlesOpen</c>) and rejects
    /// unknown positional arguments, so the <c>k7://</c> URI must be stripped before <c>Run</c>.
    /// </summary>
    public static string[] FilterCommandLine(string[] arguments) =>
        arguments
            .Where(argument => !(Uri.TryCreate(argument, UriKind.Absolute, out var uri) && IsK7Callback(uri)))
            .ToArray();

    /// <summary>
    /// <c>Exec=</c> line for the desktop entry. <c>dotnet K7.Clients.MAUI.dll</c> launches keep
    /// the host + assembly pair. App-host launches use the executable alone. <c>%u</c> passes the
    /// activating URI as a single argument. Desktop launches do not source the shell profile, so
    /// a framework-dependent app host started from a user-local SDK (<c>~/.dotnet</c>) needs
    /// <c>DOTNET_ROOT</c> on the command line or it fails with "You must install .NET".
    /// </summary>
    public static string BuildExecCommand(string processPath, string? entryAssemblyPath, string? dotnetRoot = null)
    {
        var processName = Path.GetFileNameWithoutExtension(processPath);
        var usesDotnetHost = string.Equals(processName, "dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(entryAssemblyPath);

        if (usesDotnetHost)
            return QuoteExecArgument(processPath) + " " + QuoteExecArgument(entryAssemblyPath!) + " %u";

        var prefix = string.IsNullOrWhiteSpace(dotnetRoot)
            ? string.Empty
            : "env DOTNET_ROOT=" + QuoteExecArgument(dotnetRoot) + " ";
        return prefix + QuoteExecArgument(processPath) + " %u";
    }

    /// <summary>
    /// <c>DOTNET_ROOT</c> for the desktop entry: the variable when set, else the install root
    /// derived from the runtime directory (<c>root/shared/Microsoft.NETCore.App/x.y.z/</c>).
    /// Null for self-contained apps (runtime next to the exe) or unknown layouts.
    /// </summary>
    public static string? ResolveDotnetRoot(string? environmentValue, string runtimeDirectory, string appBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue.Trim();

        var runtime = Path.TrimEndingDirectorySeparator(runtimeDirectory);
        var app = Path.TrimEndingDirectorySeparator(appBaseDirectory);
        if (string.Equals(runtime, app, StringComparison.Ordinal))
            return null;

        // .../shared/Microsoft.NETCore.App/10.0.12 -> three levels up.
        var version = Path.GetDirectoryName(runtime);
        var framework = version is null ? null : Path.GetDirectoryName(version);
        var root = framework is null ? null : Path.GetDirectoryName(framework);
        if (root is null || !string.Equals(Path.GetFileName(framework), "shared", StringComparison.Ordinal))
            return null;

        return root;
    }

    /// <param name="execCommand">Full <c>Exec=</c> value.</param>
    /// <param name="icon">Icon name (theme lookup) or absolute file path. Null omits the line.</param>
    /// <param name="applicationName">Display name.</param>
    public static string BuildDesktopEntry(string execCommand, string? icon, string applicationName = "K7")
    {
        var lines = new List<string>
        {
            "[Desktop Entry]",
            "Type=Application",
            "Name=" + applicationName,
            "Comment=K7 media server client",
            "Exec=" + execCommand,
        };

        if (!string.IsNullOrWhiteSpace(icon))
            lines.Add("Icon=" + icon);

        lines.Add("Terminal=false");
        lines.Add("Categories=AudioVideo;Video;Audio;Player;");
        lines.Add("MimeType=" + MimeType + ";");
        lines.Add("StartupNotify=true");
        lines.Add("StartupWMClass=" + ApplicationId);
        lines.Add("X-GNOME-WMClass=" + ApplicationId);
        lines.Add(string.Empty);

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Desktop Entry Specification quoting: double quotes, backslash before <c>"</c>, <c>`</c>,
    /// <c>$</c> and <c>\</c>.
    /// </summary>
    public static string QuoteExecArgument(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            if (character is '"' or '`' or '$' or '\\')
                builder.Append('\\');
            builder.Append(character);
        }

        builder.Append('"');
        return builder.ToString();
    }

    /// <summary>
    /// True when a desktop entry (other than ours) declares the <c>k7</c> scheme handler.
    /// </summary>
    public static bool DeclaresSchemeHandler(string desktopEntry)
    {
        foreach (var line in desktopEntry.Split('\n'))
        {
            if (!line.StartsWith("MimeType=", StringComparison.Ordinal))
                continue;

            var tokens = line["MimeType=".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Any(token => string.Equals(token, MimeType, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Removes the <c>k7</c> scheme handler from a foreign desktop entry so only one application
    /// answers the URI (same outcome as the single HKCU key on Windows). Returns null when the
    /// entry did not declare it.
    /// </summary>
    public static string? RemoveSchemeHandler(string desktopEntry)
    {
        if (!DeclaresSchemeHandler(desktopEntry))
            return null;

        var lines = desktopEntry.Split('\n').ToList();
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (!line.StartsWith("MimeType=", StringComparison.Ordinal))
                continue;

            var kept = line["MimeType=".Length..]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => !string.Equals(token, MimeType, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (kept.Length == 0)
                lines.RemoveAt(i);
            else
                lines[i] = "MimeType=" + string.Join(';', kept) + ";";
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Points every <c>x-scheme-handler/k7</c> association of a <c>mimeapps.list</c> to K7 and
    /// drops it from <c>[Removed Associations]</c>. Returns null when nothing changed.
    /// </summary>
    public static string? RewriteMimeAppsList(string contents)
    {
        var lines = contents.Split('\n').ToList();
        var changed = false;
        var inRemovedSection = false;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inRemovedSection = false;
                continue;
            }

            if (!line.StartsWith(MimeType + "=", StringComparison.OrdinalIgnoreCase))
                continue;

            // Section headers are above the line: find the closest one.
            for (var j = i - 1; j >= 0; j--)
            {
                var header = lines[j].Trim();
                if (header.StartsWith('[') && header.EndsWith(']'))
                {
                    inRemovedSection = string.Equals(header, "[Removed Associations]", StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }

            var expected = MimeType + "=" + DesktopFileName + ";";
            if (inRemovedSection)
            {
                lines.RemoveAt(i);
                changed = true;
            }
            else if (!string.Equals(line, expected, StringComparison.Ordinal))
            {
                lines[i] = expected;
                changed = true;
            }
        }

        return changed ? string.Join('\n', lines) : null;
    }

    /// <summary>
    /// Unix socket the primary instance listens on. <c>XDG_RUNTIME_DIR</c> is per user and
    /// wiped at logout. The temp fallback carries the user name so shared /tmp stays unique.
    /// </summary>
    public static string ResolveSocketPath(string? runtimeDirectory, string fallbackDirectory, string userName) =>
        ResolveRuntimeFile(runtimeDirectory, fallbackDirectory, userName, SocketFileName, "k7-maui-protocol", ".sock");

    /// <summary>Lock file held open by the primary instance (single instance detection).</summary>
    public static string ResolveLockPath(string? runtimeDirectory, string fallbackDirectory, string userName) =>
        ResolveRuntimeFile(runtimeDirectory, fallbackDirectory, userName, LockFileName, "k7-maui-primary", ".lock");

    /// <summary>Fallback IPC file when the socket is not reachable (same role as on Windows).</summary>
    public static string ResolveCallbackFilePath(string? runtimeDirectory, string fallbackDirectory, string userName) =>
        ResolveRuntimeFile(runtimeDirectory, fallbackDirectory, userName, CallbackFileName, "k7-maui-protocol-callback", ".uri");

    private static string ResolveRuntimeFile(
        string? runtimeDirectory,
        string fallbackDirectory,
        string userName,
        string fileName,
        string fallbackStem,
        string extension)
    {
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
            return Path.Combine(runtimeDirectory, fileName);

        var safeUser = string.IsNullOrWhiteSpace(userName) ? "user" : userName;
        return Path.Combine(fallbackDirectory, fallbackStem + "-" + safeUser + extension);
    }
}
