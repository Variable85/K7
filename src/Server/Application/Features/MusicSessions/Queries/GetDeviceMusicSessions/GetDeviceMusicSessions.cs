using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.MusicSessions.Queries.GetDeviceMusicSessions;

[Authorize]
public record GetDeviceMusicSessionsQuery : IRequest<IReadOnlyList<MusicSessionSummaryDto>>;

public class GetDeviceMusicSessionsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser) : IRequestHandler<GetDeviceMusicSessionsQuery, IReadOnlyList<MusicSessionSummaryDto>>
{
    public async Task<IReadOnlyList<MusicSessionSummaryDto>> Handle(
        GetDeviceMusicSessionsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return [];

        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        var sessions = await context.DeviceMusicSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.SharedProfileId == sharedProfileId)
            .Join(
                context.Devices.AsNoTracking(),
                session => session.DeviceId,
                device => device.Id,
                (session, device) => new { session, device.DeviceName })
            .OrderByDescending(x => x.session.UpdatedAt)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(x => DeviceMusicSessionMapper.ToSummary(x.session, x.DeviceName ?? string.Empty))
            .ToList();
    }
}
