using AidynRetold.Core.Characters;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>The Party tab's four tabs: the inventory, the stats, the skills and the spells.</summary>
public class PartyMenuTabsTests
{
    /// <summary>Alaron with every skill at 1 besides his weapon skills.</summary>
    private static PartyMenu Menu()
    {
        var party = ItemData.NewParty();
        Assert.True(party.Add(Characters.Create(Characters.Alaron() with { Skills = new SkillLevels(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1) })));
        return new PartyMenu(party, new MenuText([]), _ => null, _ => null);
    }

    /// <summary>Lets a list's slide finish, which holds off the next change.</summary>
    private static void Slid(PartyMenu menu)
    {
        for (var tick = 0; tick < (menu.SlideFrames + 1) * Cadence.MenuFrameTicks; tick++)
            menu.Step();
        Assert.Null(menu.Slide);
    }

    /// <summary>
    /// The stick's right goes to the tab on the right, from the inventory to the stats, the skills and the spells, and its left back,
    /// wrapping round as the original's four lists do, in their order.
    /// </summary>
    [Fact]
    public void TheStickStepsThroughTheFourTabsInTheirOrder()
    {
        var menu = Menu();
        var seen = new List<PartyList> { menu.List };
        for (var i = 0; i < 4; i++)
        {
            ((IPartyWindow)menu).Sideways(1);
            seen.Add(menu.List);
            Slid(menu);
        }
        Assert.Equal([PartyList.Inventory, PartyList.Stats, PartyList.Skills, PartyList.Spells, PartyList.Inventory], seen);
        Assert.Equal(PartyMenu.Tabs, seen.Take(4));

        ((IPartyWindow)menu).Sideways(-1);
        Assert.Equal(PartyList.Spells, menu.List);
        Assert.Equal(3, PartyMenu.TabOf(menu.List));
    }

    /// <summary>
    /// A click on a tab aims the stick's press at it and presses it toward the tab, so any tab is a press away, the lists sliding the
    /// way the tab is: to the left for a tab on the right, as the stick's right slides them.
    /// </summary>
    [Fact]
    public void AClickedTabIsReachedInOnePressTowardIt()
    {
        var menu = Menu();
        for (var from = 0; from < PartyMenu.Tabs.Count; from++)
        {
            for (var to = 0; to < PartyMenu.Tabs.Count; to++)
            {
                Click(menu, from);
                Slid(menu);
                Assert.Equal(from, PartyMenu.TabOf(menu.List));
                Click(menu, to);
                Assert.Equal(to, PartyMenu.TabOf(menu.List));
                Assert.Equal(from == to ? null : -Math.Sign(to - from), menu.Slide?.Direction);
                Slid(menu);
            }
        }
    }

    /// <summary>The press a click aimed is spent on it: the stick's next goes to the next tab again.</summary>
    [Fact]
    public void AClicksAimIsSpentOnItsPress()
    {
        var menu = Menu();
        Click(menu, 3);
        Slid(menu);
        ((IPartyWindow)menu).Sideways(-1);
        Assert.Equal(PartyList.Skills, menu.List);

        // An aim at a tab the window hasn't got steps as the stick does.
        Slid(menu);
        menu.AimAt(PartyList.Inventory);
        menu.ChangeList(-1, PostBattleMenu.Lists);
        Assert.Equal(PartyList.Spells, menu.List);
    }

    /// <summary>The inventory, skills and spells show 12 rows, and the stats their 11 apart with the experience lines under them.</summary>
    [Fact]
    public void TheListsShowTwelveRowsAndTheStatsTheirOwn()
    {
        var menu = Menu();
        Assert.Equal((12, 12, 12), (menu.Inventory.Visible, menu.Skills.Visible, menu.Spells.Visible));
        Assert.Equal(PartyMenu.Layout(PartyList.Stats).Visible, menu.Stats.Visible);
        Assert.Equal(menu.Stats.Rows.Count * PartyMenu.StatPitch + PartyMenu.ExperienceGap, menu.ExperienceAt);
    }

    /// <summary>The post-battle window's three lists are three tabs, the stick stepping through them as through the tab's.</summary>
    [Fact]
    public void APostBattleWindowsListsAreThreeTabs()
    {
        var menu = Menu();
        menu.ShowList(PartyList.Stats);
        var seen = new List<PartyList>();
        for (var i = 0; i < 3; i++)
        {
            menu.ChangeList(-1, PostBattleMenu.Lists);
            seen.Add(menu.List);
            Slid(menu);
        }
        Assert.Equal([PartyList.Skills, PartyList.Spells, PartyList.Stats], seen);
    }

    /// <summary>A click on tab <paramref name="tab"/>, as the view sends it.</summary>
    private static void Click(PartyMenu menu, int tab)
    {
        var step = PartyMenu.StepToTab(PartyMenu.TabOf(menu.List), tab);
        if (step == 0)
            return;
        menu.AimAt(PartyMenu.Tabs[tab]);
        ((IPartyWindow)menu).Sideways(step);
        menu.AimAt(null);
    }
}
