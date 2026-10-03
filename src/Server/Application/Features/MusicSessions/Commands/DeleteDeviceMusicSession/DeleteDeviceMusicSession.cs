using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;

namespace K7.Server.Application.Features.MusicSessions.Commands.DeleteDeviceMusicSession;

[Authorize]
public record DeleteDeviceMusicSessionCommand(Guid DeviceId) : IRequest;

public class DeleteDeviceMusicSessionCommandHandler(
    IApplicationDbContext context,
    IUser currentUser) : IRequestHandler<DeleteDeviceMusicSessionCommand>
{
    public async Task Handle(DeleteDeviceMusicSessionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        if (request.DeviceId == Guid.Empty)
            throw new ForbiddenAccessException();

        var ownsDevice = await context.Devices
            .AsNoTracking()
            .AnyAsync(d => d.Id == request.DeviceId && d.Users.Any(u => u.Id == userId), cancellationToken);
        if (!ownsDevice)
            throw new ForbiddenAccessException();

        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var session = await context.DeviceMusicSessions.FirstOrDefaultAsync(
            s => s.UserId == userId
                 && s.DeviceId == request.DeviceId
                 && s.SharedProfileId == sharedProfileId,
            cancellationToken);

        if (session is null)
            return;

        context.DeviceMusicSessions.Remove(session);
        await context.SaveChangesAsync(cancellationToken);
    }
}
