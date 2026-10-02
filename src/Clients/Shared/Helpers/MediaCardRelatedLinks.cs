using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Home;
using K7.Shared.Navigation;

namespace K7.Clients.Shared.Helpers;

public static class MediaCardRelatedLinks
{
    public static IReadOnlyList<MediaCardRelatedLink> Build(
        Guid? serieId = null,
        int? seasonNumber = null,
        Guid? albumId = null,
        Guid? artistId = null)
    {
        var links = new List<MediaCardRelatedLink>(4);

        if (IsSet(serieId))
        {
            Add(links, MediaCardRelatedKind.Series, MediaPageUrls.Build(MediaType.Serie, serieId!.Value));

            if (seasonNumber is int season)
            {
                Add(
                    links,
                    MediaCardRelatedKind.Season,
                    MediaPageUrls.Build(
                        MediaType.SerieSeason,
                        serieId.Value,
                        serieId: serieId,
                        seasonNumber: season));
            }
        }

        if (IsSet(albumId))
            Add(links, MediaCardRelatedKind.Album, MediaPageUrls.Build(MediaType.MusicAlbum, albumId!.Value));

        if (IsSet(artistId))
            Add(links, MediaCardRelatedKind.Artist, MediaPageUrls.Build(MediaType.MusicArtist, artistId!.Value));

        return links;
    }

    public static IReadOnlyList<MediaCardRelatedLink> FromLite(LiteMediaDto item) => item switch
    {
        LiteSerieEpisodeDto episode => Build(episode.SerieId, episode.SeasonNumber),
        LiteSerieSeasonDto season => Build(serieId: season.SerieId),
        LiteMusicTrackDto track => Build(albumId: track.AlbumId, artistId: track.ArtistId),
        LiteMusicAlbumDto album => Build(artistId: album.ArtistId),
        _ => []
    };

    public static IReadOnlyList<MediaCardRelatedLink> FromHome(HomeFeedItemDto item) =>
        Build(item.RelatedSerieId, item.RelatedSeasonNumber, item.RelatedAlbumId, item.RelatedArtistId);

    public static IReadOnlyList<MediaCardRelatedLink> FromMedia(MediaDto media) => media switch
    {
        SerieEpisodeDto episode => Build(episode.SerieId, episode.SeasonNumber),
        SerieSeasonDto season => Build(serieId: season.SerieId),
        MusicTrackDto track => Build(albumId: track.AlbumId, artistId: track.ArtistId),
        MusicAlbumDto album => Build(artistId: album.ArtistId),
        _ => []
    };

    public static string? ArtistHref(IReadOnlyList<MediaCardRelatedLink> links) =>
        links.FirstOrDefault(link => link.Kind == MediaCardRelatedKind.Artist)?.Href;

    public static string? SeriesHref(IReadOnlyList<MediaCardRelatedLink> links) =>
        links.FirstOrDefault(link => link.Kind == MediaCardRelatedKind.Series)?.Href;

    public static bool MatchesCard(string href, string? cardHref)
    {
        if (string.IsNullOrWhiteSpace(cardHref))
            return false;

        return string.Equals(Normalize(href), Normalize(cardHref), StringComparison.OrdinalIgnoreCase);
    }

    public static bool Same(IReadOnlyList<MediaCardRelatedLink>? left, IReadOnlyList<MediaCardRelatedLink>? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return left is null && right is null;

        return left.SequenceEqual(right);
    }

    private static void Add(List<MediaCardRelatedLink> links, MediaCardRelatedKind kind, string? href)
    {
        if (!string.IsNullOrEmpty(href))
            links.Add(new MediaCardRelatedLink(kind, href));
    }

    private static bool IsSet(Guid? id) => id is Guid value && value != Guid.Empty;

    private static string Normalize(string value)
    {
        var trimmed = value.Trim();
        var hash = trimmed.IndexOf('#');
        var path = hash >= 0 ? trimmed[..hash] : trimmed;
        var fragment = hash >= 0 ? trimmed[hash..] : "";
        return path.TrimEnd('/') + fragment;
    }
}
