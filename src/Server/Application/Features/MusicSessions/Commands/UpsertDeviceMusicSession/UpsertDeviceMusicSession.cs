using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicSessions.Commands.UpsertDeviceMusicSession;

[Authorize]
public record UpsertDeviceMusicSessionCommand(UpsertMusicSessionRequest Request) : IRequest;

public class UpsertDeviceMusicSessionCommandHandler(
    IApplicationDbContext context,
    IUser currentUser) : IRequestHandler<UpsertDeviceMusicSessionCommand>
{
    public async Task Handle(UpsertDeviceMusicSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var deviceId = request.Request.DeviceId;
        if (deviceId == Guid.Empty)
            throw new ForbiddenAccessException();

        var ownsDevice = await context.Devices
            .AsNoTracking()
            .AnyAsync(d => d.Id == deviceId && d.Users.Any(u => u.Id == userId), cancellationToken);
        if (!ownsDevice)
            throw new ForbiddenAccessException();

        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var session = await FindAsync(userId, deviceId, sharedProfileId, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (request.Request.PositionOnly)
        {
            if (session is null)
                return;

            session.PositionSeconds = request.Request.Snapshot.PositionSeconds;
            session.UpdatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (session is null)
        {
            session = new DeviceMusicSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DeviceId = deviceId,
                SharedProfileId = sharedProfileId
            };
            context.DeviceMusicSessions.Add(session);
        }

        DeviceMusicSessionMapper.Apply(session, request.Request.Snapshot, now);
        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<DeviceMusicSession?> FindAsync(
        Guid userId,
        Guid deviceId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken) =>
        context.DeviceMusicSessions.FirstOrDefaultAsync(
            s => s.UserId == userId
                 && s.DeviceId == deviceId
                 && s.SharedProfileId == sharedProfileId,
            cancellationToken);
}
