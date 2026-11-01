using AidynEnhanced.Core.Characters;
using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Tests;

/// <summary>The Party tab's three tabs and its stats and skills page.</summary>
public class PartyMenuPageTests
{
    /// <summary>Alaron with every skill at 1 besides his weapon skills, so the page is taller than the list.</summary>
    private static PartyMenu Menu()
    {
        var party = ItemData.NewParty();
        Assert.True(party.Add(Characters.Create(Characters.Alaron() with { Skills = new SkillLevels(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1) })));
        return new PartyMenu(party, new MenuText([]), _ => null, _ => null);
    }

    /// <summary>Lets a list's slide finish, which holds off the next change.</summary>
    private static void Slid(PartyMenu menu)
    {
        for (var tick = 0; tick < (PartyMenu.ListFrames + 1) * Cadence.MenuFrameTicks; tick++)
            menu.Step();
        Assert.Null(menu.Slide);
    }

    /// <summary>
    /// The stick's right goes to the tab on the right, from the inventory to the stats and skills page to the spells, and its
    /// left back, wrapping from the inventory to the spells as the original's lists wrap.
    /// </summary>
    [Fact]
    public void TheStickStepsThroughThreeTabsTheStatsAndSkillsOnOnePage()
    {
        var menu = Menu();
        var seen = new List<PartyList> { menu.List };
        for (var i = 0; i < 3; i++)
        {
            ((IPartyWindow)menu).Sideways(1);
            seen.Add(menu.List);
            Slid(menu);
        }
        Assert.Equal([PartyList.Inventory, PartyList.Stats, PartyList.Spells, PartyList.Inventory], seen);

        ((IPartyWindow)menu).Sideways(-1);
        Assert.Equal(PartyList.Spells, menu.List);
        Assert.Equal(2, PartyMenu.TabOf(menu.List));
    }

    /// <summary>The page's cursor runs from the last stat into the first skill, and from the first stat up to the last skill.</summary>
    [Fact]
    public void ThePagesCursorRunsFromTheStatsIntoTheSkillsAndWrapsRound()
    {
        var menu = Menu();
        menu.ShowList(PartyList.Stats);
        var skills = menu.Skills.Rows.Count;

        menu.MoveCursor(5);
        Assert.Equal((PartyList.Stats, 5), (menu.List, menu.Stats.Cursor));
        menu.MoveCursor(1);
        Assert.Equal((PartyList.Skills, 0), (menu.List, menu.Skills.Cursor));
        Assert.True(menu.OnPage);
        menu.MoveCursor(-1);
        Assert.Equal((PartyList.Stats, 5), (menu.List, menu.Stats.Cursor));

        menu.MoveCursor(-6);
        Assert.Equal((PartyList.Skills, skills - 1), (menu.List, menu.Skills.Cursor));
        menu.MoveCursor(1);
        Assert.Equal((PartyList.Stats, 0), (menu.List, menu.Stats.Cursor));
    }

    /// <summary>
    /// The page scrolls a row at a time, only as far as shows the cursor's row whole: the first three skills show under the stats,
    /// the fourth scrolls the first stat away, and back on the first stat it's at the top again.
    /// </summary>
    [Fact]
    public void ThePageScrollsARowAtATimeToShowTheCursorsRow()
    {
        var menu = Menu();
        menu.ShowList(PartyList.Stats);
        Assert.Equal(menu.ExperienceAt + PartyMenu.ExperienceLines * PartyMenu.ExperiencePitch + PartyMenu.SkillsGap, menu.SkillsAt);

        menu.MoveCursor(6 + 2);
        Assert.Equal((PartyList.Skills, 2, 0), (menu.List, menu.Skills.Cursor, menu.PageScroll));
        Assert.True(menu.PageMoreBelow);
        menu.MoveCursor(1);
        Assert.Equal(PartyMenu.StatPitch, menu.PageScroll);
        Assert.True(menu.PageMoreAbove);

        // Up to the first skill keeps the scroll, as its row and the experience lines over it show.
        menu.MoveCursor(-3);
        Assert.Equal((0, PartyMenu.StatPitch), (menu.Skills.Cursor, menu.PageScroll));
        menu.MoveCursor(-6);
        Assert.Equal((PartyList.Stats, 0, 0), (menu.List, menu.Stats.Cursor, menu.PageScroll));

        // The last skill scrolls the page to show its foot and no further.
        menu.MoveCursor(-1);
        Assert.False(menu.PageMoreBelow);
        Assert.InRange(menu.PageHeight - menu.PageScroll, PartyMenu.ListHeight - PartyMenu.SkillPitch, PartyMenu.ListHeight);
    }

    /// <summary>The page's rows are found under a point as they're scrolled, and a row not shown whole isn't.</summary>
    [Fact]
    public void ThePagesRowsAreFoundUnderAPointAsTheyreScrolled()
    {
        var menu = Menu();
        menu.ShowList(PartyList.Stats);
        var list = PartyMenu.ListBounds;

        Assert.Equal((PartyList.Stats, 1), menu.PageRowAt(list.Left + 10, list.Top + PartyMenu.StatPitch + 2));
        Assert.Equal((PartyList.Skills, 0), menu.PageRowAt(list.Left + 10, list.Top + menu.SkillsAt + 2));
        // The fourth skill runs past the list's foot.
        Assert.Null(menu.PageRowAt(list.Left + 10, list.Top + menu.SkillsAt + 3 * PartyMenu.SkillPitch + 2));

        menu.PointPage(PartyList.Skills, 3);
        Assert.Equal((PartyList.Skills, 3, PartyMenu.StatPitch), (menu.List, menu.Skills.Cursor, menu.PageScroll));
        // The first stat has scrolled away, and the second is at the top.
        Assert.Equal((PartyList.Stats, 1), menu.PageRowAt(list.Left + 10, list.Top + 2));
    }

    /// <summary>The stick comes back onto the page where it left it, in the skills or the stats.</summary>
    [Fact]
    public void TheStickComesBackToThePageWhereItLeftIt()
    {
        var menu = Menu();
        menu.ShowList(PartyList.Stats);
        menu.MoveCursor(6);
        Assert.Equal(PartyList.Skills, menu.List);

        ((IPartyWindow)menu).Sideways(1);
        Assert.Equal(PartyList.Spells, menu.List);
        Slid(menu);
        ((IPartyWindow)menu).Sideways(-1);
        Assert.Equal(PartyList.Skills, menu.List);
    }

    /// <summary>The lists show 12 rows, and the page's lists all theirs, as the page scrolls rather than they.</summary>
    [Fact]
    public void TheListsShowTwelveRowsAndThePageScrollsAsAWhole()
    {
        var menu = Menu();
        Assert.Equal((12, 12), (menu.Inventory.Visible, menu.Spells.Visible));
        Assert.Equal((int.MaxValue, int.MaxValue), (menu.Stats.Visible, menu.Skills.Visible));
    }

    /// <summary>The post-battle window's three lists are two tabs, the spells and the stats and skills page.</summary>
    [Fact]
    public void APostBattleWindowsListsAreTwoTabs()
    {
        var menu = Menu();
        menu.ShowList(PartyList.Stats);

        menu.ChangeList(1, PostBattleMenu.Lists);
        Assert.Equal(PartyList.Spells, menu.List);
        Slid(menu);
        menu.ChangeList(1, PostBattleMenu.Lists);
        Assert.Equal(PartyList.Stats, menu.List);
    }
}
