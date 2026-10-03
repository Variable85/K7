namespace K7.Clients.MAUI.Linux;

/// <summary>
/// OIDC redirect mode for the Linux client. Default is the custom scheme (<c>k7://</c>, same as
/// Windows). <c>K7_AUTH_REDIRECT=loopback</c> switches the authorize request to the OpenIddict
/// loopback listener (<c>http://localhost:port/</c>, registered as <c>http://localhost/</c> on
/// the server) for browsers whose sandbox cannot launch host applications for custom schemes.
/// </summary>
public static class LinuxAuthRedirect
{
    public const string EnvironmentVariable = "K7_AUTH_REDIRECT";
    public const string LoopbackValue = "loopback";

    public static readonly Uri CustomSchemeUri = new("k7://callback/login", UriKind.Absolute);
    public static readonly Uri LoopbackUri = new("http://localhost/", UriKind.Absolute);

    public static bool UsesCustomScheme(string? environmentValue) =>
        !string.Equals(environmentValue?.Trim(), LoopbackValue, StringComparison.OrdinalIgnoreCase);

    public static Uri Resolve(string? environmentValue) =>
        UsesCustomScheme(environmentValue) ? CustomSchemeUri : LoopbackUri;
}
