using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.MusicSessions.Commands.DeleteDeviceMusicSession;
using K7.Server.Application.Features.MusicSessions.Commands.UpsertDeviceMusicSession;
using K7.Server.Application.Features.MusicSessions.Queries.GetDeviceMusicSession;
using K7.Server.Application.Features.MusicSessions.Queries.GetDeviceMusicSessions;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Users;

public class MusicSessionEndpoints : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/users/me/music-sessions", async (
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetDeviceMusicSessionsQuery(), cancellationToken);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName("GetMusicSessions")
        .WithTags(groupName);

        endpointRouteBuilder.MapGet("/api/users/me/music-sessions/{deviceId:guid}", async (
            Guid deviceId,
            [FromServices] ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetDeviceMusicSessionQuery(deviceId), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName("GetMusicSession")
        .WithTags(groupName);

        endpointRouteBuilder.MapPut("/api/users/me/music-session", async (
            [FromBody] UpsertMusicSessionRequest request,
            [FromServices] ISender sender,
            [FromServices] IUser user,
            [FromServices] IMusicSessionNotifier musicSessions,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new UpsertDeviceMusicSessionCommand(request), cancellationToken);
            if (!request.PositionOnly)
                await NotifyMusicSessionsAsync(user, musicSessions, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName("UpsertMusicSession")
        .WithTags(groupName);

        endpointRouteBuilder.MapDelete("/api/users/me/music-session", async (
            [FromQuery] Guid deviceId,
            [FromServices] ISender sender,
            [FromServices] IUser user,
            [FromServices] IMusicSessionNotifier musicSessions,
            CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteDeviceMusicSessionCommand(deviceId), cancellationToken);
            await NotifyMusicSessionsAsync(user, musicSessions, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization(Policies.GuestOrAbove)
        .WithName("DeleteMusicSession")
        .WithTags(groupName);
    }

    private static Task NotifyMusicSessionsAsync(
        IUser user,
        IMusicSessionNotifier musicSessions,
        CancellationToken cancellationToken)
    {
        var identityUserId = user.IdentityId;
        return string.IsNullOrEmpty(identityUserId)
            ? Task.CompletedTask
            : musicSessions.NotifyChangedAsync(identityUserId, cancellationToken);
    }
}
