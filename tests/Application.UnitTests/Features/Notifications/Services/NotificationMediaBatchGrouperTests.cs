using K7.Server.Application.Features.Notifications.Services;
using AwesomeAssertions;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services;

[TestFixture]
public class NotificationMediaBatchGrouperTests
{
    [Test]
    public void Aggregate_ShouldKeepSingleEpisode_AsEpisode()
    {
        var items = new[]
        {
            Episode("serie-1", "Show A", season: 1, episode: 2)
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int> { ["serie-1"] = 3 });

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Episode");
        result[0]["Media.Type"].Should().Be("SerieEpisode");
        result[0]["Batch.Count"].Should().Be(1);
        result[0]["Season.Number"].Should().Be(1);
        result[0]["Episode.Number"].Should().Be(2);
    }

    [Test]
    public void Aggregate_ShouldGroupSameSeasonEpisodes_AsSeason_WhenSerieHasMultipleSeasons()
    {
        var items = new[]
        {
            Episode("serie-1", "Show A", season: 2, episode: 1),
            Episode("serie-1", "Show A", season: 2, episode: 2),
            Episode("serie-1", "Show A", season: 2, episode: 3)
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int> { ["serie-1"] = 5 });

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Season");
        result[0]["Media.Type"].Should().Be("SerieSeason");
        result[0]["Batch.Count"].Should().Be(3);
        result[0]["Season.Number"].Should().Be(2);
        result[0].Should().NotContainKey("Episode.Number");
    }

    [Test]
    public void Aggregate_ShouldGroupMultiSeasonEpisodes_AsSerie()
    {
        var items = new[]
        {
            Episode("serie-1", "Show A", season: 1, episode: 1),
            Episode("serie-1", "Show A", season: 2, episode: 1)
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int> { ["serie-1"] = 4 });

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Serie");
        result[0]["Media.Type"].Should().Be("Serie");
        result[0]["Batch.Count"].Should().Be(2);
    }

    [Test]
    public void Aggregate_ShouldGroupTracks_AsAlbum()
    {
        var items = new[]
        {
            Track("album-1", "Album A", "Track 1"),
            Track("album-1", "Album A", "Track 2")
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int>());

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Album");
        result[0]["Media.Type"].Should().Be("MusicAlbum");
        result[0]["Batch.Count"].Should().Be(2);
        result[0]["Media.Title"].Should().Be("Album A");
    }

    [Test]
    public void Aggregate_ShouldGroupSeasons_UnderSerie()
    {
        var items = new[]
        {
            Season("serie-1", "Show A", season: 1),
            Season("serie-1", "Show A", season: 2)
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int> { ["serie-1"] = 4 });

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Serie");
        result[0]["Media.Type"].Should().Be("Serie");
        result[0]["Batch.Count"].Should().Be(2);
    }

    [Test]
    public void Aggregate_ShouldKeepSingleSeason_AsSeason()
    {
        var items = new[]
        {
            Season("serie-1", "Show A", season: 3)
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int> { ["serie-1"] = 5 });

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Season");
        result[0]["Media.Type"].Should().Be("SerieSeason");
        result[0]["Season.Number"].Should().Be(3);
    }

    [Test]
    public void Aggregate_ShouldGroupAlbums_UnderArtist()
    {
        var items = new[]
        {
            Album("artist-1", "Artist A", "Album 1"),
            Album("artist-1", "Artist A", "Album 2")
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int>());

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Artist");
        result[0]["Media.Type"].Should().Be("MusicArtist");
        result[0]["Batch.Count"].Should().Be(2);
        result[0]["Media.Title"].Should().Be("Artist A");
    }

    [Test]
    public void Aggregate_ShouldKeepSingleAlbum_AsAlbum()
    {
        var items = new[]
        {
            Album("artist-1", "Artist A", "Album 1")
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int>());

        result.Should().HaveCount(1);
        result[0]["Batch.Kind"].Should().Be("Album");
        result[0]["Media.Type"].Should().Be("MusicAlbum");
        result[0]["Media.Title"].Should().Be("Album 1");
    }

    [Test]
    public void Aggregate_ShouldPassThroughMovies_WithoutBatchMeta()
    {
        var items = new[]
        {
            Movie("Movie 1"),
            Movie("Movie 2")
        };

        var result = NotificationMediaBatchGrouper.Aggregate(items, new Dictionary<string, int>());

        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => !r.ContainsKey("Batch.Kind"));
    }

    private static Dictionary<string, object?> Episode(string serieId, string show, int season, int episode) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Media.Type"] = "SerieEpisode",
            ["Media.Title"] = $"E{episode}",
            ["Serie.Id"] = serieId,
            ["Show.Name"] = show,
            ["Season.Number"] = season,
            ["Episode.Number"] = episode
        };

    private static Dictionary<string, object?> Season(string serieId, string show, int season) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Media.Type"] = "SerieSeason",
            ["Media.Title"] = $"Season {season}",
            ["Serie.Id"] = serieId,
            ["Show.Name"] = show,
            ["Season.Number"] = season
        };

    private static Dictionary<string, object?> Track(string albumId, string album, string track) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Media.Type"] = "MusicTrack",
            ["Media.Title"] = track,
            ["Album.Id"] = albumId,
            ["Album.Name"] = album,
            ["Track.Name"] = track
        };

    private static Dictionary<string, object?> Album(string artistId, string artist, string album) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Media.Type"] = "MusicAlbum",
            ["Media.Title"] = album,
            ["Album.Id"] = Guid.NewGuid().ToString(),
            ["Album.Name"] = album,
            ["Artist.Id"] = artistId,
            ["Artist.Name"] = artist
        };

    private static Dictionary<string, object?> Movie(string title) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Media.Type"] = "Movie",
            ["Media.Title"] = title
        };
}
