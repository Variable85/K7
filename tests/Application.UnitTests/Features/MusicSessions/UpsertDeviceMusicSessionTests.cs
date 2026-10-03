using AwesomeAssertions;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.MusicSessions.Commands.UpsertDeviceMusicSession;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.MusicSessions;

[TestFixture]
public class UpsertDeviceMusicSessionTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private UpsertDeviceMusicSessionCommandHandler _handler = null!;

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

        _currentUser = Substitute.For<IUser>();
        _handler = new UpsertDeviceMusicSessionCommandHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenDeviceBelongsToAnotherUser()
    {
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var owner = new User { Id = ownerId, IdentityUserId = "owner", DisplayName = "Owner" };
        var caller = new User { Id = callerId, IdentityUserId = "caller", DisplayName = "Caller" };
        _context.Users.AddRange(owner, caller);
        _context.Devices.Add(new Device
        {
            Id = deviceId,
            ClientType = ClientType.Web,
            DeviceName = "Other phone",
            Users = [owner]
        });
        await _context.SaveChangesAsync();

        _currentUser.Id.Returns(callerId);

        var act = () => _handler.Handle(
            new UpsertDeviceMusicSessionCommand(new UpsertMusicSessionRequest
            {
                DeviceId = deviceId,
                Snapshot = new MusicSessionSnapshotDto()
            }),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _context.DeviceMusicSessions.Should().BeEmpty();
    }
}
