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

    /// <summary>
    /// The original's look lays the lists out where the original's are, eight rows of 13 from (160, 101), sliding 140, and the inventory a
    /// list again with what's worn in it; the port's look puts them back.
    /// </summary>
    [Fact]
    public void TheOriginalLookHasTheOriginalsLists()
    {
        var (menu, _) = Menu(showWorn: false);
        menu.Grid = false;
        menu.ShowWorn = true;
        menu.OriginalLook = true;

        Assert.Equal(PartyMenu.OriginalList, menu.LayoutOf(PartyList.Inventory).Bounds);
        Assert.Equal((8, 8, 1), (menu.Inventory.Visible, menu.Spells.Visible, menu.Inventory.Columns));
        Assert.Equal(PartyMenu.OriginalListDistance, menu.SlideDistance);
        Assert.Equal(Sword.Name, menu.Inventory.Rows[0].Item.Name);
        menu.OriginalLook = false;
        Assert.Equal((PartyMenu.Layout(PartyList.Spells).Visible, PartyMenu.ListDistance), (menu.Spells.Visible, menu.SlideDistance));
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
    /// tab's grid fills the list's frame, 8 lines of 7, the user's choice (Nov 2, 2026).
    /// </summary>
    [Fact]
    public void TheStickMovesAlongTheGridAndOffItsEndOntoTheTabs()
    {
        var (menu, _) = Menu(showWorn: false);
        IPartyWindow window = menu;
        Assert.Equal((7, 56), (menu.Inventory.Columns, menu.Inventory.Visible));

        window.Sideways(1);
        window.MoveCursor(1);
        Assert.Equal(8, menu.Inventory.Cursor);
        for (var i = 0; i < 5; i++)
            window.Sideways(1);
        Assert.Equal((PartyList.Inventory, 13), (menu.List, menu.Inventory.Cursor));

        window.Sideways(1);
        Assert.Equal(PartyList.Stats, menu.List);
        Assert.Equal(13, menu.Inventory.Cursor);
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
    /// A click on a slot of the silhouette selects what's worn there, and only A, or a double click, takes it off, as A on its row would;
    /// a slot with nothing in it isn't selected, and moving the cursor lets the slot go, A then being the cursor's row's again.
    /// </summary>
    [Fact]
    public void ASelectedSlotsItemIsWhatATakesOff()
    {
        var (menu, member) = Menu(showWorn: false);
        menu.SelectSlot(EquipmentSlot.Helmet);
        Assert.Null(menu.SelectedSlot);

        menu.SelectSlot(EquipmentSlot.Weapon);
        Assert.Equal(EquipmentSlot.Weapon, menu.SelectedSlot);
        Assert.Equal(Sword.Id, member.Equipment.Weapon?.Id);
        Assert.Equal(Sword.Name, menu.Details()?.Title);
        menu.Accept();
        Assert.Null(member.Equipment.Weapon);
        Assert.Null(menu.SelectedSlot);
        Assert.Contains(menu.Inventory.Rows, r => r.Item.Id == Sword.Id && r.Wearer is null);

        Assert.Equal(EquipRefusal.None, menu.Party.Equip(member, Sword.Id));
        menu.Refresh();
        menu.SelectSlot(EquipmentSlot.Weapon);
        menu.MoveCursor(1);
        Assert.Null(menu.SelectedSlot);
    }

    /// <summary>Taking an item off with no room in the inventory leaves it on and says so, where the original says nothing.</summary>
    [Fact]
    public void TakingAnItemOffWithNoRoomSaysSo()
    {
        var member = PotionData.Member("Alaron", 0, null);
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(Things.Length), [Sword, .. Things]), null, [member]);
        Assert.True(party.Inventory.Add(Sword.Id));
        Assert.Equal(EquipRefusal.None, party.Equip(member, Sword.Id));
        foreach (var thing in Things)
            Assert.True(party.Inventory.Add(thing.Id));
        var menu = new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true };

        menu.SelectSlot(EquipmentSlot.Weapon);
        menu.Accept();

        Assert.Equal(Sword.Id, member.Equipment.Weapon?.Id);
        Assert.Equal(PartyMenu.NoRoom, menu.Message?.Text);
    }

    /// <summary>
    /// The actiontip on a worn slot offers Unequip and Details; the stick moves its cursor, not the grid's, and choosing Unequip takes
    /// the item off as A would. B closes it, and with it up the member and the list stay.
    /// </summary>
    [Fact]
    public void TheActionTipOnAWornItemUnequipsIt()
    {
        var (menu, member) = Menu(showWorn: false);
        menu.SelectSlot(EquipmentSlot.Weapon);
        Assert.True(menu.OpenTip());

        Assert.Equal([ActionTipChoice.Unequip, ActionTipChoice.Details], menu.Tip!.Choices.Rows);
        Assert.True(menu.HasPopup);
        var cursor = menu.Inventory.Cursor;
        menu.MoveCursor(1);
        Assert.Equal((1, cursor, EquipmentSlot.Weapon), (menu.Tip.Choices.Cursor, menu.Inventory.Cursor, menu.SelectedSlot));
        Assert.True(menu.Back());
        Assert.Null(menu.Tip);

        menu.OpenTip();
        menu.Accept();
        Assert.Null(menu.Tip);
        Assert.Null(member.Equipment.Weapon);
        Assert.Contains(menu.Inventory.Rows, r => r.Item.Id == Sword.Id && r.Wearer is null);
    }

    /// <summary>
    /// The actiontip on a carried item names what A does, Equip for what can be worn, and its Details hold the details open until A or B
    /// lets them go; the menus' Appraise opens it on the cursor's item, the controller's way. A plain item with no details has none.
    /// </summary>
    [Fact]
    public void TheActionTipNamesWhatADoesAndHoldsTheDetails()
    {
        var (menu, member) = Menu(showWorn: false);
        menu.Point(menu.Inventory.Rows.ToList().FindIndex(r => r.Item.Id == Things[0].Id));
        Assert.False(menu.OpenTip());
        Assert.True(menu.Party.Unequip(member, member.Equipment.Weapon!));
        menu.Refresh();
        menu.Point(menu.Inventory.Rows.ToList().FindIndex(r => r.Item.Id == Sword.Id));
        Assert.True(((IPartyWindow)menu).Appraise());
        Assert.Equal([ActionTipChoice.Equip, ActionTipChoice.Details], menu.Tip!.Choices.Rows);
        menu.MoveCursor(1);
        menu.Accept();
        Assert.Null(menu.Tip);
        Assert.True(menu.DetailsPane is { Showing: true, Pinned: true });
        menu.Step();
        menu.MoveCursor(1);
        Assert.True(menu.DetailsPane.Showing);
        Assert.True(menu.Back());
        Assert.False(menu.DetailsPane.Pinned);

        menu.OriginalLook = true;
        Assert.False(menu.OpenTip());
    }

    /// <summary>The wheel scrolls a grid a line at a time, no further than shows its first line or its last, leaving the cursor where it is.</summary>
    [Fact]
    public void ScrollingMovesTheViewNotTheCursor()
    {
        var list = new MenuList<int>([.. Enumerable.Range(0, 30)], 16) { Columns = 8 };
        list.ScrollLines(1);
        Assert.Equal((8, 0), (list.First, list.Cursor));
        list.ScrollLines(5);
        Assert.Equal(16, list.First);
        list.ScrollLines(-9);
        Assert.Equal(0, list.First);
    }

    /// <summary>
    /// With the mouse in use nothing on the grid is selected until a click selects a cell, the user's choice (Nov 3, 2026), and a
    /// change of member lets the cell go; with a key or a controller the cursor always shows.
    /// </summary>
    [Fact]
    public void WithTheMouseNothingIsSelectedUntilAClick()
    {
        var alaron = PotionData.Member("Alaron", 0, null);
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(40), Things), null, alaron, PotionData.Member("Brenna", 1, null));
        foreach (var thing in Things)
            Assert.True(party.Inventory.Add(thing.Id));
        var menu = new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true };

        Assert.True(menu.CursorShows);
        menu.Pointing = true;
        Assert.False(menu.CursorShows);
        menu.Point(5);
        Assert.True(menu.CursorShows);
        menu.ChangeMember(1);
        Assert.False(menu.CursorShows);
    }

    /// <summary>The first press on the hidden cursor from a key or a controller only shows it, where it is; the next moves it.</summary>
    [Fact]
    public void AKeyFirstShowsTheHiddenCursor()
    {
        var (menu, _) = Menu(showWorn: false);
        menu.Pointing = true;

        Assert.False(menu.ShowCursor(MenuAction.MemberLeft));
        Assert.True(menu.ShowCursor(MenuAction.Accept));
        Assert.True(menu.CursorShows);
        Assert.Equal(0, menu.Inventory.Cursor);
        Assert.False(menu.ShowCursor(MenuAction.Down));
    }

    /// <summary>The wheel scrolls the grid: the hidden cursor goes with the view, and one a click put on a cell stays on its item.</summary>
    [Fact]
    public void TheWheelScrollsTheGrid()
    {
        ItemRecord[] many = [.. Enumerable.Range(20, 84).Select(n => ItemData.NewItem(n))];
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(128), many), null, PotionData.Member("Alaron", 0, null));
        foreach (var thing in many)
            Assert.True(party.Inventory.Add(thing.Id));
        var menu = new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true, Pointing = true };
        var line = menu.Inventory.Columns;

        menu.ScrollGrid(2);
        Assert.Equal((2 * line, 2 * line), (menu.Inventory.First, menu.Inventory.Cursor));
        menu.Point(2 * line + 1);
        menu.ScrollGrid(1);
        Assert.Equal((3 * line, 2 * line + 1), (menu.Inventory.First, menu.Inventory.Cursor));
    }
}
