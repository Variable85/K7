namespace K7.Server.Application.Common.Interfaces;

public interface IMusicSessionNotifier
{
    Task NotifyChangedAsync(string identityUserId, CancellationToken cancellationToken = default);
}
