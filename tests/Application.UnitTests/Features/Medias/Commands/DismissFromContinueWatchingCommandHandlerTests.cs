using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Commands.DismissFromContinueWatching;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Samples;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class DismissFromContinueWatchingCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private PlaybackBookmarkService _bookmarks = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
    private Guid _userId;
    private Guid _episode1Id;
    private Guid _episode2Id;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "viewer" });

        var serie = new Serie { Id = Guid.NewGuid(), Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        serie.Seasons.Add(season);

        _episode1Id = Guid.NewGuid();
        _episode2Id = Guid.NewGuid();
        var episode1 = CreateEpisode(_episode1Id, serie, season, 1);
        var episode2 = CreateEpisode(_episode2Id, serie, season, 2);

        _context.Medias.AddRange(serie, season, episode1, episode2);
        var (libraryId, peerServerId) = RemoteIndexedFilesSamples.EnsureLibraryAndPeer(_context);
        _context.RemoteIndexedFiles.AddRange(
            RemoteIndexedFilesSamples.Create(_episode1Id, libraryId, peerServerId),
            RemoteIndexedFilesSamples.Create(_episode2Id, libraryId, peerServerId));
        _context.SaveChanges();

        _bookmarks = new PlaybackBookmarkService(_context, NullLogger<PlaybackBookmarkService>.Instance);
        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldRemoveSharedProfileBookmarks_WhenSharedProfileIsActive()
    {
        var sharedProfileId = await AddSharedProfileAsync();
        var timeNow = DateTime.UtcNow;
        await _bookmarks.OnEpisodeCompletedAsync(_userId, sharedProfileId: null, _episode1Id, timeNow);
        await _bookmarks.OnEpisodeCompletedAsync(userId: null, sharedProfileId, _episode1Id, timeNow);
        await _bookmarks.UpsertItemBookmarkAsync(userId: null, sharedProfileId, _episode2Id, 30, 1200, timeNow);
        await _context.SaveChangesAsync();

        await CreateHandler(sharedProfileId).Handle(
            new DismissFromContinueWatchingCommand(_episode2Id),
            CancellationToken.None);

        (await _context.PlaybackBookmarks.CountAsync(b => b.SharedProfileId == sharedProfileId)).Should().Be(0);
        (await _context.PlaybackBookmarks.CountAsync(b => b.UserId == _userId)).Should().Be(1);
        _cacheInvalidator.Received(1).InvalidateAll();
    }

    [Test]
    public async Task Handle_ShouldRemovePersonalBookmarks_WhenNoSharedProfileIsActive()
    {
        var sharedProfileId = await AddSharedProfileAsync();
        var timeNow = DateTime.UtcNow;
        await _bookmarks.OnEpisodeCompletedAsync(_userId, sharedProfileId: null, _episode1Id, timeNow);
        await _bookmarks.OnEpisodeCompletedAsync(userId: null, sharedProfileId, _episode1Id, timeNow);
        await _context.SaveChangesAsync();

        await CreateHandler(sharedProfileId: null).Handle(
            new DismissFromContinueWatchingCommand(_episode2Id),
            CancellationToken.None);

        (await _context.PlaybackBookmarks.CountAsync(b => b.UserId == _userId)).Should().Be(0);
        (await _context.PlaybackBookmarks.CountAsync(b => b.SharedProfileId == sharedProfileId)).Should().Be(1);
    }

    private DismissFromContinueWatchingCommandHandler CreateHandler(Guid? sharedProfileId)
    {
        var currentUser = Substitute.For<IUser>();
        currentUser.Id.Returns(_userId);
        currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(sharedProfileId);

        return new DismissFromContinueWatchingCommandHandler(
            _context,
            currentUser,
            Substitute.For<IMediaAccessGuard>(),
            _bookmarks,
            _cacheInvalidator);
    }

    private async Task<Guid> AddSharedProfileAsync()
    {
        var sharedProfileId = Guid.NewGuid();
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();
        return sharedProfileId;
    }

    private static SerieEpisode CreateEpisode(Guid id, Serie serie, SerieSeason season, int number) =>
        new()
        {
            Id = id,
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = number,
            Title = $"E{number}",
            SortTitle = $"E{number}"
        };
}
