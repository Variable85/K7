namespace K7.Server.Application.Features.Notifications.Services;

/// <summary>
/// Groups MediaAdded/MediaCreated notification payloads the same way the home
/// recently-added feed does: episodes/seasons under a serie, tracks under an album,
/// albums under an artist.
/// </summary>
public static class NotificationMediaBatchGrouper
{
    public static IReadOnlyList<IReadOnlyDictionary<string, object?>> Aggregate(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> items,
        IReadOnlyDictionary<string, int> serieSeasonCounts)
    {
        if (items.Count == 0)
            return [];

        var result = new List<(int Order, IReadOnlyDictionary<string, object?> Data)>();
        var serieGroups = new Dictionary<string, (int Order, List<IReadOnlyDictionary<string, object?>> Items)>(
            StringComparer.OrdinalIgnoreCase);
        var albumTrackGroups = new Dictionary<string, (int Order, List<IReadOnlyDictionary<string, object?>> Items)>(
            StringComparer.OrdinalIgnoreCase);
        var artistAlbumGroups = new Dictionary<string, (int Order, List<IReadOnlyDictionary<string, object?>> Items)>(
            StringComparer.OrdinalIgnoreCase);
        var insertOrder = 0;

        foreach (var item in items)
        {
            var mediaType = GetString(item, "Media.Type");

            if (mediaType is "SerieEpisode" or "SerieSeason"
                && GetString(item, "Serie.Id") is { Length: > 0 } serieId)
            {
                if (!serieGroups.ContainsKey(serieId))
                    serieGroups[serieId] = (insertOrder++, []);
                serieGroups[serieId].Items.Add(item);
                continue;
            }

            if (string.Equals(mediaType, "MusicTrack", StringComparison.OrdinalIgnoreCase)
                && GetString(item, "Album.Id") is { Length: > 0 } albumId)
            {
                if (!albumTrackGroups.ContainsKey(albumId))
                    albumTrackGroups[albumId] = (insertOrder++, []);
                albumTrackGroups[albumId].Items.Add(item);
                continue;
            }

            if (string.Equals(mediaType, "MusicAlbum", StringComparison.OrdinalIgnoreCase)
                && GetString(item, "Artist.Id") is { Length: > 0 } artistId)
            {
                if (!artistAlbumGroups.ContainsKey(artistId))
                    artistAlbumGroups[artistId] = (insertOrder++, []);
                artistAlbumGroups[artistId].Items.Add(item);
                continue;
            }

            // Movie / Serie / MusicArtist (and albums without artist) send as-is.
            result.Add((insertOrder++, Clone(item)));
        }

        foreach (var (serieId, (order, groupItems)) in serieGroups)
        {
            serieSeasonCounts.TryGetValue(serieId, out var seasonCount);
            if (seasonCount <= 0)
                seasonCount = 1;
            result.Add((order, AggregateSerieLeaves(groupItems, seasonCount)));
        }

        foreach (var (_, (order, groupItems)) in albumTrackGroups)
            result.Add((order, AggregateAlbumTracks(groupItems)));

        foreach (var (_, (order, groupItems)) in artistAlbumGroups)
            result.Add((order, AggregateArtistAlbums(groupItems)));

        return result.OrderBy(x => x.Order).Select(x => x.Data).ToList();
    }

    private static IReadOnlyDictionary<string, object?> AggregateSerieLeaves(
        List<IReadOnlyDictionary<string, object?>> leaves,
        int serieSeasonCount)
    {
        var episodes = leaves
            .Where(i => string.Equals(GetString(i, "Media.Type"), "SerieEpisode", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var seasons = leaves
            .Where(i => string.Equals(GetString(i, "Media.Type"), "SerieSeason", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (episodes.Count > 0 && seasons.Count == 0)
            return AggregateSerieEpisodes(episodes, serieSeasonCount);

        if (seasons.Count > 0 && episodes.Count == 0)
            return AggregateSerieSeasons(seasons, serieSeasonCount);

        // Mixed episode + season adds for the same show collapse to a serie card.
        var first = leaves[0];
        var showName = GetString(first, "Show.Name") ?? GetString(first, "Media.Title") ?? "";
        var data = Clone(first);
        data["Show.Name"] = showName;
        data["Media.Title"] = showName;
        data["Media.Type"] = "Serie";
        data["Batch.Kind"] = "Serie";
        data["Batch.Count"] = leaves.Count;
        data.Remove("Season.Number");
        data.Remove("Episode.Number");
        data.Remove("Episode.Name");
        return data;
    }

    private static IReadOnlyDictionary<string, object?> AggregateSerieEpisodes(
        List<IReadOnlyDictionary<string, object?>> episodes,
        int serieSeasonCount)
    {
        var first = episodes[0];
        var showName = GetString(first, "Show.Name") ?? GetString(first, "Media.Title") ?? "";
        var distinctSeasons = episodes
            .Select(e => GetInt(e, "Season.Number"))
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();
        var isSingleSeason = distinctSeasons.Count == 1;
        var serieHasMultipleSeasons = serieSeasonCount > 1;

        var data = Clone(first);
        data["Show.Name"] = showName;
        data["Media.Title"] = showName;
        data["Batch.Count"] = episodes.Count;

        if (episodes.Count == 1)
        {
            data["Media.Type"] = "SerieEpisode";
            data["Batch.Kind"] = "Episode";
            return data;
        }

        if (isSingleSeason && serieHasMultipleSeasons)
        {
            data["Media.Type"] = "SerieSeason";
            data["Batch.Kind"] = "Season";
            data["Season.Number"] = distinctSeasons[0];
            data.Remove("Episode.Number");
            data.Remove("Episode.Name");
            return data;
        }

        data["Media.Type"] = "Serie";
        data["Batch.Kind"] = "Serie";
        data.Remove("Season.Number");
        data.Remove("Episode.Number");
        data.Remove("Episode.Name");
        return data;
    }

    private static IReadOnlyDictionary<string, object?> AggregateSerieSeasons(
        List<IReadOnlyDictionary<string, object?>> seasons,
        int serieSeasonCount)
    {
        var first = seasons[0];
        var showName = GetString(first, "Show.Name") ?? GetString(first, "Media.Title") ?? "";
        var data = Clone(first);
        data["Show.Name"] = showName;
        data["Media.Title"] = showName;
        data["Batch.Count"] = seasons.Count;

        var distinctSeasons = seasons
            .Select(s => GetInt(s, "Season.Number"))
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

        if (seasons.Count == 1 || (distinctSeasons.Count == 1 && serieSeasonCount > 1))
        {
            data["Media.Type"] = "SerieSeason";
            data["Batch.Kind"] = "Season";
            if (distinctSeasons.Count == 1)
                data["Season.Number"] = distinctSeasons[0];
            return data;
        }

        data["Media.Type"] = "Serie";
        data["Batch.Kind"] = "Serie";
        data.Remove("Season.Number");
        return data;
    }

    private static IReadOnlyDictionary<string, object?> AggregateAlbumTracks(
        List<IReadOnlyDictionary<string, object?>> tracks)
    {
        var first = tracks[0];
        var data = Clone(first);
        var albumName = GetString(first, "Album.Name") ?? GetString(first, "Media.Title") ?? "";
        data["Media.Title"] = albumName;
        data["Album.Name"] = albumName;
        data["Media.Type"] = "MusicAlbum";
        data["Batch.Kind"] = "Album";
        data["Batch.Count"] = tracks.Count;
        data.Remove("Track.Name");
        data.Remove("Track.Number");
        return data;
    }

    private static IReadOnlyDictionary<string, object?> AggregateArtistAlbums(
        List<IReadOnlyDictionary<string, object?>> albums)
    {
        var first = albums[0];
        var data = Clone(first);
        var artistName = GetString(first, "Artist.Name") ?? "";
        data["Batch.Count"] = albums.Count;

        if (albums.Count == 1)
        {
            var albumName = GetString(first, "Album.Name") ?? GetString(first, "Media.Title") ?? "";
            data["Media.Title"] = albumName;
            data["Album.Name"] = albumName;
            data["Media.Type"] = "MusicAlbum";
            data["Batch.Kind"] = "Album";
            return data;
        }

        data["Media.Title"] = string.IsNullOrWhiteSpace(artistName)
            ? GetString(first, "Media.Title") ?? ""
            : artistName;
        data["Artist.Name"] = artistName;
        data["Media.Type"] = "MusicArtist";
        data["Batch.Kind"] = "Artist";
        data.Remove("Album.Name");
        data.Remove("Album.Id");
        return data;
    }

    private static Dictionary<string, object?> Clone(IReadOnlyDictionary<string, object?> source)
    {
        var copy = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
            copy[key] = value;
        return copy;
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> data, string key) =>
        data.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static int? GetInt(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
            return null;
        if (value is int i)
            return i;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }
}
