namespace K7.Shared;

/// <summary>
/// Keeps a persisted music queue small. Sourced sessions store one previous track,
/// the current track, and a few upcoming tracks. Ad-hoc sessions store the whole
/// queue up to <see cref="AdHocCap"/>, preferring the current track and what follows.
/// </summary>
public static class MusicSessionWindow
{
    public const int SourcedUpcomingCap = 8;
    public const int AdHocCap = 100;

    public static List<T> Trim<T>(IReadOnlyList<T> items, int currentIndex, bool hasSource) =>
        Slice(items, currentIndex, hasSource).Items;

    public static (List<T> Items, int CurrentIndex) Slice<T>(IReadOnlyList<T> items, int currentIndex, bool hasSource)
    {
        if (items.Count == 0 || currentIndex < 0 || currentIndex >= items.Count)
            return ([], -1);

        if (!hasSource)
        {
            if (items.Count <= AdHocCap)
                return ([.. items], currentIndex);

            var kept = new List<T> { items[currentIndex] };
            for (var forward = currentIndex + 1; kept.Count < AdHocCap && forward < items.Count; forward++)
                kept.Add(items[forward]);

            var prefix = new List<T>();
            for (var back = currentIndex - 1; kept.Count + prefix.Count < AdHocCap && back >= 0; back--)
                prefix.Add(items[back]);

            prefix.Reverse();
            var index = prefix.Count;
            prefix.AddRange(kept);
            return (prefix, index);
        }

        var start = Math.Max(0, currentIndex - 1);
        var end = Math.Min(items.Count - 1, currentIndex + SourcedUpcomingCap);
        return (items.Skip(start).Take(end - start + 1).ToList(), currentIndex - start);
    }
}
