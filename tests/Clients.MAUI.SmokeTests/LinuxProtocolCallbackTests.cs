using K7.Clients.MAUI.Linux;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class LinuxProtocolCallbackTests
{
    [Test]
    public void IsK7Callback_ShouldAcceptCustomSchemeOnly()
    {
        LinuxProtocolCallback.IsK7Callback(new Uri("k7://callback/login?code=abc")).Should().BeTrue();
        LinuxProtocolCallback.IsK7Callback(new Uri("http://localhost:9/")).Should().BeFalse();
    }

    [Test]
    public void TryGetCommandLineCallback_ShouldReturnFirstK7Uri_WhenPresent()
    {
        var callback = LinuxProtocolCallback.TryGetCommandLineCallback(
            ["/opt/k7/K7.Clients.MAUI", "--verbose", "k7://callback/login?code=abc&state=x"]);

        callback.Should().Be(new Uri("k7://callback/login?code=abc&state=x"));
    }

    [Test]
    public void TryGetCommandLineCallback_ShouldReturnNull_WhenNoK7Uri()
    {
        LinuxProtocolCallback.TryGetCommandLineCallback(["/opt/k7/K7.Clients.MAUI", "https://k7.example.com"])
            .Should().BeNull();
    }

    [Test]
    public void FilterCommandLine_ShouldStripK7UrisOnly()
    {
        LinuxProtocolCallback.FilterCommandLine(["--verbose", "k7://callback/login?code=abc", "file.txt"])
            .Should().Equal("--verbose", "file.txt");
    }

    [Test]
    public void BuildExecCommand_ShouldPassUriToAppHost()
    {
        LinuxProtocolCallback.BuildExecCommand("/opt/k7/K7.Clients.MAUI", "/opt/k7/K7.Clients.MAUI.dll")
            .Should().Be("\"/opt/k7/K7.Clients.MAUI\" %u");
    }

    [Test]
    public void BuildExecCommand_ShouldExportDotnetRoot_WhenRuntimeIsUserLocal()
    {
        LinuxProtocolCallback.BuildExecCommand("/opt/k7/K7.Clients.MAUI", "/opt/k7/K7.Clients.MAUI.dll", "/home/alice/.dotnet")
            .Should().Be("env DOTNET_ROOT=\"/home/alice/.dotnet\" \"/opt/k7/K7.Clients.MAUI\" %u");
    }

    [Test]
    public void ResolveDotnetRoot_ShouldPreferEnvironment_ThenRuntimeLayout_ThenNull()
    {
        LinuxProtocolCallback.ResolveDotnetRoot(" /usr/lib/dotnet ", "/x/shared/Microsoft.NETCore.App/10.0.12/", "/opt/k7")
            .Should().Be("/usr/lib/dotnet");
        // Path.GetDirectoryName uses the host separator; the smoke tests run on Windows.
        LinuxProtocolCallback.ResolveDotnetRoot(null, "/home/alice/.dotnet/shared/Microsoft.NETCore.App/10.0.12/", "/opt/k7")!
            .Replace('\\', '/').Should().Be("/home/alice/.dotnet");
        LinuxProtocolCallback.ResolveDotnetRoot(null, "/opt/k7/", "/opt/k7")
            .Should().BeNull();
        LinuxProtocolCallback.ResolveDotnetRoot(null, "/weird/layout/10.0.12/", "/opt/k7")
            .Should().BeNull();
    }

    [Test]
    public void BuildExecCommand_ShouldKeepAssembly_WhenLaunchedThroughDotnetHost()
    {
        LinuxProtocolCallback.BuildExecCommand("/usr/share/dotnet/dotnet", "/src/bin/K7.Clients.MAUI.dll")
            .Should().Be("\"/usr/share/dotnet/dotnet\" \"/src/bin/K7.Clients.MAUI.dll\" %u");
    }

    [Test]
    public void QuoteExecArgument_ShouldEscapeDesktopEntryReservedCharacters()
    {
        LinuxProtocolCallback.QuoteExecArgument("/opt/my \"app\"/$bin`x\\y")
            .Should().Be("\"/opt/my \\\"app\\\"/\\$bin\\`x\\\\y\"");
    }

    [Test]
    public void BuildDesktopEntry_ShouldDeclareSchemeHandlerAndWmClass()
    {
        var entry = LinuxProtocolCallback.BuildDesktopEntry("\"/opt/k7/K7.Clients.MAUI\" %u", "/opt/k7/hicolor/scalable/apps/appicon.svg");
        var lines = entry.Split('\n');

        lines[0].Should().Be("[Desktop Entry]");
        lines.Should().Contain("Exec=\"/opt/k7/K7.Clients.MAUI\" %u");
        lines.Should().Contain("MimeType=x-scheme-handler/k7;");
        lines.Should().Contain("Icon=/opt/k7/hicolor/scalable/apps/appicon.svg");
        lines.Should().Contain("StartupWMClass=com.k7.maui");
        entry.Should().EndWith("\n");
    }

    [Test]
    public void BuildDesktopEntry_ShouldOmitIcon_WhenMissing()
    {
        LinuxProtocolCallback.BuildDesktopEntry("\"/opt/k7/K7.Clients.MAUI\" %u", null)
            .Should().NotContain("Icon=");
    }

    [Test]
    public void ResolveSocketPath_ShouldPreferRuntimeDir_AndFallBackToPerUserTemp()
    {
        LinuxProtocolCallback.ResolveSocketPath("/run/user/1000", "/tmp", "alice")
            .Should().Be(Path.Combine("/run/user/1000", "k7-maui-protocol.sock"));

        LinuxProtocolCallback.ResolveSocketPath(null, "/tmp", "alice")
            .Should().Be(Path.Combine("/tmp", "k7-maui-protocol-alice.sock"));
    }

    [Test]
    public void ResolveLockAndCallbackPaths_ShouldLiveNextToTheSocket()
    {
        LinuxProtocolCallback.ResolveLockPath("/run/user/1000", "/tmp", "alice")
            .Should().Be(Path.Combine("/run/user/1000", "k7-maui-primary.lock"));
        LinuxProtocolCallback.ResolveCallbackFilePath(null, "/tmp", "alice")
            .Should().Be(Path.Combine("/tmp", "k7-maui-protocol-callback-alice.uri"));
    }

    [Test]
    public void RemoveSchemeHandler_ShouldStripK7Token_AndKeepOtherMimeTypes()
    {
        var entry = "[Desktop Entry]\nName=Other\nExec=other %u\nMimeType=x-scheme-handler/k7;text/html;\n";

        LinuxProtocolCallback.RemoveSchemeHandler(entry)
            .Should().Be("[Desktop Entry]\nName=Other\nExec=other %u\nMimeType=text/html;\n");
    }

    [Test]
    public void RemoveSchemeHandler_ShouldDropMimeTypeLine_WhenK7WasTheOnlyEntry()
    {
        var entry = "[Desktop Entry]\nName=Old K7\nMimeType=x-scheme-handler/k7;\nTerminal=false\n";

        LinuxProtocolCallback.RemoveSchemeHandler(entry)
            .Should().Be("[Desktop Entry]\nName=Old K7\nTerminal=false\n");
    }

    [Test]
    public void RemoveSchemeHandler_ShouldReturnNull_WhenEntryDoesNotClaimK7()
    {
        LinuxProtocolCallback.RemoveSchemeHandler("[Desktop Entry]\nName=Browser\nMimeType=text/html;x-scheme-handler/http;\n")
            .Should().BeNull();
    }

    [Test]
    public void RewriteMimeAppsList_ShouldPointEveryK7AssociationToK7_AndDropRemovedOnes()
    {
        var contents = string.Join('\n',
            "[Default Applications]",
            "x-scheme-handler/k7=firefox.desktop",
            "text/html=firefox.desktop",
            "",
            "[Added Associations]",
            "x-scheme-handler/k7=other.desktop;com.k7.maui.desktop;",
            "",
            "[Removed Associations]",
            "x-scheme-handler/k7=com.k7.maui.desktop;",
            "");

        LinuxProtocolCallback.RewriteMimeAppsList(contents).Should().Be(string.Join('\n',
            "[Default Applications]",
            "x-scheme-handler/k7=com.k7.maui.desktop;",
            "text/html=firefox.desktop",
            "",
            "[Added Associations]",
            "x-scheme-handler/k7=com.k7.maui.desktop;",
            "",
            "[Removed Associations]",
            ""));
    }

    [Test]
    public void LinuxAuthRedirect_ShouldDefaultToCustomScheme_AndOptIntoLoopback()
    {
        LinuxAuthRedirect.Resolve(null).Should().Be(new Uri("k7://callback/login"));
        LinuxAuthRedirect.Resolve("k7").Should().Be(new Uri("k7://callback/login"));
        LinuxAuthRedirect.Resolve(" Loopback ").Should().Be(new Uri("http://localhost/"));
        LinuxAuthRedirect.UsesCustomScheme(null).Should().BeTrue();
        LinuxAuthRedirect.UsesCustomScheme("loopback").Should().BeFalse();
    }

    [Test]
    public void RewriteMimeAppsList_ShouldReturnNull_WhenAlreadyPointingToK7()
    {
        LinuxProtocolCallback.RewriteMimeAppsList("[Default Applications]\nx-scheme-handler/k7=com.k7.maui.desktop;\n")
            .Should().BeNull();
    }
}
