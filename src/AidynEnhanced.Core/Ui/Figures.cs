using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>The figure drawn behind the Party tab's silhouette slots, the user's art (Nov 1, 2026): a man's, a big man's or a woman's.</summary>
public enum Figure
{
    Man,
    LargeMan,
    Woman,
}

/// <summary>Which figure a member is drawn over, the user's choice (Nov 1, 2026).</summary>
public static class Figures
{
    /// <summary>The big men, by portrait, as the voices are: Abrecan, Arturo, Baird and Becan.</summary>
    private static readonly HashSet<RecordId> LargeMen = [.. new[] { 0x0106, 0x010B, 0x010C, 0x0113 }.Select(i => RecordId.Hex(RecordTypes.Image, i))];

    /// <summary>
    /// A member's figure, by their entity's record: a woman's for a woman's voice, a big man's for the big men and a deep voice,
    /// and a man's for the rest, and for no record.
    /// </summary>
    public static Figure Of(DbRecord? record) => record switch
    {
        { Voice: Voice.Female } => Figure.Woman,
        { Voice: Voice.Deep } => Figure.LargeMan,
        { Portrait: { } portrait } when LargeMen.Contains(portrait) => Figure.LargeMan,
        _ => Figure.Man,
    };
}
