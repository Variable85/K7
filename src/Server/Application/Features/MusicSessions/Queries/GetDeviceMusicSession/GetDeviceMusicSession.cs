using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicSessions.Queries.GetDeviceMusicSession;

[Authorize]
public record GetDeviceMusicSessionQuery(Guid DeviceId) : IRequest<MusicSessionSnapshotDto?>;

public class GetDeviceMusicSessionQueryHandler(
    IApplicationDbContext context,
    IUser currentUser) : IRequestHandler<GetDeviceMusicSessionQuery, MusicSessionSnapshotDto?>
{
    public async Task<MusicSessionSnapshotDto?> Handle(
        GetDeviceMusicSessionQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId || request.DeviceId == Guid.Empty)
            return null;

        var ownsDevice = await context.Devices
            .AsNoTracking()
            .AnyAsync(d => d.Id == request.DeviceId && d.Users.Any(u => u.Id == userId), cancellationToken);
        if (!ownsDevice)
            return null;

        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var session = await context.DeviceMusicSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.UserId == userId
                     && s.DeviceId == request.DeviceId
                     && s.SharedProfileId == sharedProfileId,
                cancellationToken);

        return session is null ? null : DeviceMusicSessionMapper.ToSnapshot(session);
    }
}
