using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace K7.Server.Web.Services;

internal sealed class MusicSessionNotifier(IHubContext<K7Hub, IK7HubClient> hubContext) : IMusicSessionNotifier
{
    public Task NotifyChangedAsync(string identityUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(identityUserId))
            return Task.CompletedTask;

        return hubContext.Clients.Group(identityUserId).ReceiveMusicSessionsChanged();
    }
}
