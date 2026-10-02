namespace K7.Clients.Shared.Models;

public enum MediaCardRelatedKind
{
    Series,
    Season,
    Album,
    Artist
}

public sealed record MediaCardRelatedLink(MediaCardRelatedKind Kind, string Href);
