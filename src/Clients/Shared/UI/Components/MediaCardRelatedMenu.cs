using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.UI.Components;

internal static class MediaCardRelatedMenu
{
    public static string Icon(MediaCardRelatedKind kind) => kind switch
    {
        MediaCardRelatedKind.Series => "television",
        MediaCardRelatedKind.Season => "rows",
        MediaCardRelatedKind.Album => "vinyl-record",
        MediaCardRelatedKind.Artist => "user",
        _ => "arrow-square-out"
    };

    public static string LabelKey(MediaCardRelatedKind kind) => kind switch
    {
        MediaCardRelatedKind.Series => "GoToSeries",
        MediaCardRelatedKind.Season => "GoToSeason",
        MediaCardRelatedKind.Album => "GoToAlbum",
        MediaCardRelatedKind.Artist => "GoToArtist",
        _ => "GoToSeries"
    };

    public static IReadOnlyList<MediaCardRelatedLink> Visible(
        IReadOnlyList<MediaCardRelatedLink>? links,
        string? cardHref)
    {
        if (links is null || links.Count == 0)
            return [];

        var visible = new List<MediaCardRelatedLink>(links.Count);
        foreach (var link in links)
        {
            if (!MediaCardRelatedLinks.MatchesCard(link.Href, cardHref))
                visible.Add(link);
        }

        return visible;
    }
}
