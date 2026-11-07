using System.Text;
using AidynRetold.Plugins;

namespace AidynRetold.Core.Camp;

/// <summary>
/// The seed a camp's arrangement and its talk are chosen by: a stable hash of the world day, where the party camps and who's in
/// it, so reopening the camp the same day in the same place shows the same arrangement, a new day may show another, and nothing
/// depends on the runtime's own hashing, which changes between sessions.
/// </summary>
public static class CampSeed
{
    /// <param name="day">The world day, as the clock counts them from the start of the year.</param>
    /// <param name="location">Where the party camps: the chunk's key, such as <c>aidyn:mapset/0/M07</c>.</param>
    /// <param name="party">The active members, in any order.</param>
    public static uint Of(int day, string location, IEnumerable<RecordId> party)
    {
        var members = string.Join(",", party.Select(p => p.ToString()).OrderBy(s => s, StringComparer.Ordinal));
        return Fnv1a($"{day}|{location}|{members}");
    }

    /// <summary>FNV-1a over the text's UTF-8, 32 bits: the same everywhere, every run.</summary>
    public static uint Fnv1a(string text)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash;
    }
}
