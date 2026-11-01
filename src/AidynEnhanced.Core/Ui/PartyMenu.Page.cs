namespace AidynEnhanced.Core.Ui;

public sealed partial class PartyMenu
{
    /// <summary>
    /// The stats and skills page, the user's design (Oct 31, 2026): the stats' rows 11 apart as the
    /// original's stats list has them, 4 under them the experience lines 11 apart, and 6 under those the skills' rows, 13 apart as the
    /// skills list's. The page scrolls as a whole, a row at a time, the experience lines going with the first skill.
    /// </summary>
    public const int StatPitch = 11, ExperienceGap = 4, ExperiencePitch = 11, ExperienceLines = 3, SkillsGap = 6, SkillPitch = 13;

    /// <summary>The tabs over the list, left to right: the inventory, the stats and skills page, and the spells.</summary>
    public static readonly IReadOnlyList<PartyList> Tabs = [PartyList.Inventory, PartyList.Stats, PartyList.Spells];

    /// <summary>The section of the page the stick last left it on, which it comes back to.</summary>
    private PartyList _pageSection = PartyList.Stats;

    /// <summary>The list a tab shows, the stats for the stats and skills page.</summary>
    public static PartyList Page(PartyList list) => list == PartyList.Skills ? PartyList.Stats : list;

    /// <summary>Which of <see cref="Tabs"/> shows a list.</summary>
    public static int TabOf(PartyList list) => Tabs.ToList().IndexOf(Page(list));

    /// <summary>Whether the list showing is the stats and skills page.</summary>
    public bool OnPage => Page(List) == PartyList.Stats;

    /// <summary>How tall the list is, which the page shows of.</summary>
    public static int ListHeight => ListBounds.Bottom - ListBounds.Top;

    /// <summary>Where on the page, from its top, the experience lines are.</summary>
    public int ExperienceAt => Stats.Rows.Count * StatPitch + ExperienceGap;

    /// <summary>Where on the page, from its top, the skills' rows start.</summary>
    public int SkillsAt => ExperienceAt + ExperienceLines * ExperiencePitch + SkillsGap;

    /// <summary>How tall the page is, to the last skill's foot.</summary>
    public int PageHeight => SkillsAt + Skills.Rows.Count * SkillPitch;

    /// <summary>How far the page is scrolled up from its top, always to a row's top or the experience lines'.</summary>
    public int PageScroll { get; private set; }

    /// <summary>Whether the page has rows hidden above, or below.</summary>
    public bool PageMoreAbove => PageScroll > 0;

    public bool PageMoreBelow => PageHeight > PageScroll + ListHeight;

    /// <summary>Where a stat's row is on the list, from its bounds' top, and a skill's, as the page is scrolled.</summary>
    public int StatTop(int row) => row * StatPitch - PageScroll;

    public int SkillTop(int row) => SkillsAt + row * SkillPitch - PageScroll;

    /// <summary>Whether a row from so far down the list, so tall, shows whole within its bounds.</summary>
    public static bool PageShows(int top, int height) => top >= 0 && top + height <= ListHeight;

    /// <summary>The stats' or skills' row under a point in the frame, if the page shows it whole.</summary>
    public (PartyList List, int Row)? PageRowAt(float x, float y)
    {
        var bounds = ListBounds;
        if (x < bounds.Left || x >= bounds.Right || y < bounds.Top || y >= bounds.Bottom)
            return null;
        var at = (int)(y - bounds.Top);
        for (var i = 0; i < Stats.Rows.Count; i++)
        {
            if (at >= StatTop(i) && at < StatTop(i) + StatPitch && PageShows(StatTop(i), StatPitch))
                return (PartyList.Stats, i);
        }
        for (var i = 0; i < Skills.Rows.Count; i++)
        {
            if (at >= SkillTop(i) && at < SkillTop(i) + SkillPitch && PageShows(SkillTop(i), SkillPitch))
                return (PartyList.Skills, i);
        }
        return null;
    }

    /// <summary>Puts the cursor on a row of the page, in either section, as hovering does.</summary>
    public void PointPage(PartyList section, int row)
    {
        if (Message is not null || DetailsPane.Showing || Crafting is not null || !OnPage)
            return;
        List = section;
        if (section == PartyList.Skills)
            Skills.Point(row);
        else
            Stats.Point(row);
        KeepPage();
    }

    /// <summary>Shows a tab's list, or the page at the section it was left on.</summary>
    private void ShowPage(PartyList list)
    {
        if (OnPage)
            _pageSection = List;
        List = list == PartyList.Stats ? _pageSection : list;
        KeepPage();
    }

    /// <summary>Moves the page's cursor a row, from the last stat to the first skill and back, wrapping round.</summary>
    private void MovePage(int by)
    {
        var stats = Stats.Rows.Count;
        var count = stats + Skills.Rows.Count;
        if (count == 0)
            return;
        var at = List == PartyList.Skills ? stats + Skills.Cursor : Stats.Cursor;
        at = ((at + by) % count + count) % count;
        if (at < stats)
        {
            List = PartyList.Stats;
            Stats.Point(at);
        }
        else
        {
            List = PartyList.Skills;
            Skills.Point(at - stats);
        }
        KeepPage();
    }

    /// <summary>
    /// Scrolls the page only as far as it must to show the cursor's row whole, the first skill's with the experience lines over it,
    /// and no further than shows its foot.
    /// </summary>
    private void KeepPage()
    {
        var height = ListHeight;
        var (top, bottom) = List == PartyList.Skills && Skills.Rows.Count > 0
            ? (Skills.Cursor == 0 ? ExperienceAt : SkillsAt + Skills.Cursor * SkillPitch, SkillsAt + (Skills.Cursor + 1) * SkillPitch)
            : (Stats.Cursor * StatPitch, (Stats.Cursor + 1) * StatPitch);
        var stops = PageStops().ToList();
        var scroll = PageScroll;
        if (top < scroll)
            scroll = stops.LastOrDefault(s => s <= top);
        else if (bottom > scroll + height)
            scroll = stops.FirstOrDefault(s => s >= bottom - height, stops[^1]);
        // No further than shows the foot.
        var last = stops.FirstOrDefault(s => s >= PageHeight - height, stops[^1]);
        PageScroll = Math.Clamp(scroll, 0, Math.Max(0, last));
    }

    /// <summary>Where the page may be scrolled to: each row's top, and the experience lines'.</summary>
    private IEnumerable<int> PageStops()
    {
        for (var i = 0; i < Stats.Rows.Count; i++)
            yield return i * StatPitch;
        yield return ExperienceAt;
        for (var i = 0; i < Skills.Rows.Count; i++)
            yield return SkillsAt + i * SkillPitch;
    }
}
