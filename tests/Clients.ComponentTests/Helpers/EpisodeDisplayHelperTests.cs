using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class EpisodeDisplayHelperTests
{
    [Test]
    public void FormatAirDate_ShouldPreferAirDate_OverReleaseDate()
    {
        var episode = Episode(airDate: new DateOnly(2008, 1, 20), releaseDate: new DateOnly(2010, 1, 1));

        EpisodeDisplayHelper.FormatAirDate(episode).Should().Contain("2008");
        EpisodeDisplayHelper.GetAirDate(episode).Should().Be(new DateOnly(2008, 1, 20));
    }

    [Test]
    public void FormatDuration_ShouldUseRuntimeMinutes()
    {
        var episode = Episode(runtime: 47, durationSeconds: 3600);

        EpisodeDisplayHelper.FormatDuration(episode).Should().Be("47min");
    }

    [Test]
    public void FormatRating_ShouldMatchEpisodePageScore()
    {
        var episode = Episode() with { Rating = 8.2 };

        EpisodeDisplayHelper.FormatRating(episode).Should().MatchRegex(@"^8[.,]2$");
    }

    [Test]
    public void FormatDuration_ShouldFallBackToFileDuration_WhenRuntimeMissing()
    {
        var episode = Episode(durationSeconds: 3720);

        EpisodeDisplayHelper.FormatDuration(episode).Should().Be("1h02");
    }

    private static LiteSerieEpisodeDto Episode(
        DateOnly? airDate = null,
        DateOnly? releaseDate = null,
        int? runtime = null,
        double? durationSeconds = null) => new()
    {
        Id = Guid.NewGuid(),
        EpisodeNumber = 1,
        SeasonNumber = 1,
        SerieId = Guid.NewGuid(),
        AirDate = airDate,
        ReleaseDate = releaseDate,
        Runtime = runtime,
        Duration = durationSeconds
    };
}
