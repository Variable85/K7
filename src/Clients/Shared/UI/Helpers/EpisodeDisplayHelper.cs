using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.UI.Helpers;

public static class EpisodeDisplayHelper
{
    public static DateOnly? GetAirDate(LiteSerieEpisodeDto episode) =>
        episode.AirDate ?? episode.ReleaseDate;

    public static string? FormatAirDate(LiteSerieEpisodeDto episode) =>
        GetAirDate(episode)?.ToString("d MMMM yyyy");

    public static string? FormatDuration(LiteSerieEpisodeDto episode)
    {
        if (episode.Runtime is > 0)
            return FormatMinutes(episode.Runtime.Value);

        if (episode.Duration is >= 30)
            return FormatMinutes((int)Math.Round(episode.Duration.Value / 60d));

        return null;
    }

    public static string? FormatRating(LiteSerieEpisodeDto episode) =>
        episode.Rating is > 0 ? episode.Rating.Value.ToString("N1") : null;

    public static string FormatMinutes(int totalMinutes)
    {
        if (totalMinutes >= 60)
        {
            var hours = totalMinutes / 60;
            var mins = totalMinutes % 60;
            return mins > 0 ? $"{hours}h{mins:00}" : $"{hours}h";
        }

        return $"{totalMinutes}min";
    }
}
