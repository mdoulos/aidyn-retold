using AidynEnhanced.Core.Characters;
using AidynEnhanced.Core.Items;
using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Tests;

/// <summary>The Party tab's inventory grid: its cursor, its scrolling, what's worn in it, and the silhouette's slots under the mouse.</summary>
public class PartyMenuGridTests
{
    private static readonly WeaponRecord Sword = ItemData.NewWeapon(1, WeaponClass.Sword);

    /// <summary>Twenty different things to carry, so the grid has three lines.</summary>
    private static readonly ItemRecord[] Things = [.. Enumerable.Range(20, 20).Select(n => ItemData.NewItem(n))];

    /// <summary>Alaron carrying the twenty things and wearing the sword, on the tab's grid.</summary>
    private static (PartyMenu Menu, Character Member) Menu(bool showWorn)
    {
        var member = PotionData.Member("Alaron", 0, null);
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(40), [Sword, .. Things]), null, [member]);
        Assert.True(party.Inventory.Add(Sword.Id));
        Assert.Equal(EquipRefusal.None, party.Equip(member, Sword.Id));
        foreach (var thing in Things)
            Assert.True(party.Inventory.Add(thing.Id));
        var menu = new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true, ShowWorn = showWorn };
        return (menu, member);
    }

    /// <summary>A grid scrolls a line at a time, as far as shows the cursor's line, and no further than shows the last.</summary>
    [Fact]
    public void AGridScrollsALineAtATime()
    {
        var list = new MenuList<int>([.. Enumerable.Range(0, 30)], 16) { Columns = 8 };
        list.Point(15);
        Assert.Equal(0, list.First);
        list.Point(16);
        Assert.Equal(8, list.First);
        list.Point(29);
        Assert.Equal(16, list.First);
        list.Point(3);
        Assert.Equal(0, list.First);
    }

    /// <summary>
    /// The stick steps along a line and off its ends, and up and down a line, wrapping from the last line to the first and back in the
    /// same column, and onto the last thing where the line it reaches is short of the column.
    /// </summary>
    [Theory]
    [InlineData(0, 1, 0, 1)]
    [InlineData(7, 1, 0, null)]
    [InlineData(8, -1, 0, null)]
    [InlineData(19, 1, 0, null)]
    [InlineData(3, 0, 1, 11)]
    [InlineData(13, 0, 1, 19)]
    [InlineData(17, 0, 1, 1)]
    [InlineData(5, 0, -1, 19)]
    [InlineData(2, 0, -1, 18)]
    public void AStepGoesAlongALineOrUpAndDown(int cursor, int across, int down, int? to) =>
        Assert.Equal(to, GridLayout.Step(cursor, 20, 8, across, down));

    /// <summary>
    /// On the grid the stick's left and right move along the line, and off its end onto the tab beside; up and down move a line. The
    /// tab's grid fills the list's frame, 9 lines of 8.
    /// </summary>
    [Fact]
    public void TheStickMovesAlongTheGridAndOffItsEndOntoTheTabs()
    {
        var (menu, _) = Menu(showWorn: false);
        IPartyWindow window = menu;
        Assert.Equal((8, 72), (menu.Inventory.Columns, menu.Inventory.Visible));

        window.Sideways(1);
        window.MoveCursor(1);
        Assert.Equal(9, menu.Inventory.Cursor);
        for (var i = 0; i < 6; i++)
            window.Sideways(1);
        Assert.Equal((PartyList.Inventory, 15), (menu.List, menu.Inventory.Cursor));

        window.Sideways(1);
        Assert.Equal(PartyList.Stats, menu.List);
        Assert.Equal(15, menu.Inventory.Cursor);
    }

    /// <summary>
    /// With what's worn hidden, the grid holds only what's carried, and the sword comes back into it as it's taken off; shown, the
    /// sword's cell is the wearer's, first.
    /// </summary>
    [Fact]
    public void WornItemsShowInTheGridOnlyWhenTheyreShown()
    {
        var (menu, _) = Menu(showWorn: false);
        Assert.Equal(20, menu.Inventory.Rows.Count);
        Assert.All(menu.Inventory.Rows, r => Assert.Null(r.Wearer));

        menu.ShowWorn = true;
        Assert.Equal(21, menu.Inventory.Rows.Count);
        Assert.Equal((Sword.Id, (int?)0), (menu.Inventory.Rows[0].Item.Id, menu.Inventory.Rows[0].Wearer));
    }

    /// <summary>
    /// A click on a slot of the silhouette aims A at what's worn there, so it comes off as A on its row takes it off; the aim is spent on
    /// the one press, and a slot with nothing in it does nothing.
    /// </summary>
    [Fact]
    public void AClickOnTheSilhouettesSlotTakesOffWhatsWornThere()
    {
        var (menu, member) = Menu(showWorn: false);
        menu.Point(3);
        menu.AimAtSlot(EquipmentSlot.Helmet);
        menu.Accept();
        Assert.Equal(Sword.Id, member.Equipment.Weapon?.Id);

        menu.AimAtSlot(EquipmentSlot.Weapon);
        menu.Accept();
        Assert.Null(member.Equipment.Weapon);
        Assert.Contains(menu.Inventory.Rows, r => r.Item.Id == Sword.Id && r.Wearer is null);
        Assert.Equal(21, menu.Inventory.Rows.Count);
    }
}
