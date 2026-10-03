using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MauiNativeVideoChromeTests
{
    [Test]
    public void IsEnabledFor_ShouldFollowHostFlag_ForDirectPlayAndHls()
    {
        MauiNativeVideoChrome.EnableForNativeMediaElementHosts();

        MauiNativeVideoChrome.IsEnabledFor("video/x-matroska", "https://k7.example/direct-stream")
            .Should().BeTrue();
        MauiNativeVideoChrome.IsEnabledFor("application/vnd.apple.mpegurl", "https://k7.example/master.m3u8")
            .Should().BeTrue();
        MauiNativeVideoChrome.IsEnabledFor(null, null)
            .Should().Be(MauiNativeVideoChrome.IsEnabled);
    }
}
