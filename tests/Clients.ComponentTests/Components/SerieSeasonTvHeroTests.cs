using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class SerieSeasonTvHeroTests
{
    [Test]
    public void Render_ShouldShowAirDateAndDuration_WhenEpisodeHasMetadata()
    {
        using var ctx = CreateContext();

        var cut = ctx.Render<SerieSeasonTvHero>(p => p
            .Add(h => h.SerieId, Guid.NewGuid().ToString())
            .Add(h => h.Episode, new LiteSerieEpisodeDto
            {
                Id = Guid.NewGuid(),
                Title = "Pilot",
                EpisodeNumber = 1,
                SeasonNumber = 1,
                SerieId = Guid.NewGuid(),
                AirDate = new DateOnly(2008, 1, 20),
                Runtime = 47,
                Rating = 8.2
            }));

        var meta = cut.Find(".tv-episode-meta").InnerHtml;
        meta.IndexOf("tv-episode-rating", StringComparison.Ordinal)
            .Should().BeLessThan(meta.IndexOf("tv-episode-date", StringComparison.Ordinal));
        meta.IndexOf("tv-episode-date", StringComparison.Ordinal)
            .Should().BeLessThan(meta.IndexOf("tv-episode-duration", StringComparison.Ordinal));
        cut.Find(".tv-episode-date").TextContent.Should().Contain("2008");
        cut.Find(".tv-episode-meta-sep").TextContent.Trim().Should().Be("\u2022");
        cut.Find(".tv-episode-duration").TextContent.Should().Be("47min");
        cut.Find(".tv-episode-rating").TextContent.Should().MatchRegex(@"8[.,]2");
        cut.Find(".tv-episode-rating").ClassList.Should().Contain("k7-chip--primary");
    }

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<IK7ServerService>());
        ctx.Services.AddSingleton(CreateLocalizer<SerieSeason>());
        ctx.Services.AddSingleton(CreateLocalizer<SharedResource>());
        return ctx;
    }

    private static IStringLocalizer<T> CreateLocalizer<T>()
    {
        var localizer = Substitute.For<IStringLocalizer<T>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        return localizer;
    }
}
