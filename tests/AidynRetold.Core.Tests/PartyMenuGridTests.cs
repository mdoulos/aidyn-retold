using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

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
    /// On the grid the stick's left and right move along the line and up and down a line, and at an edge go to what's that way on the
    /// tab, the user's choice (Nov 3, 2026): right stops, down goes onto the filter and order under it, left onto the silhouette and up onto
    /// the tabs. The tab's grid fills the list's frame, 8 lines of 7, the user's choice (Nov 2, 2026).
    /// </summary>
    [Fact]
    public void TheStickMovesAlongTheGridAndOffItsEdgesOntoWhatsBeside()
    {
        var (menu, _) = Menu(showWorn: false);
        IPartyWindow window = menu;
        Assert.Equal((6, 36), (menu.Inventory.Columns, menu.Inventory.Visible));

        // Six to a line (Nov 6, 2026; seven before): down from the second cell is the eighth, right runs to the line's end and stops.
        window.Sideways(1);
        window.MoveCursor(1);
        Assert.Equal(7, menu.Inventory.Cursor);
        for (var i = 0; i < 6; i++)
            window.Sideways(1);
        Assert.Equal((PartyList.Inventory, 11, PartyFocus.List), (menu.List, menu.Inventory.Cursor, menu.Focus));
        window.MoveCursor(1);
        Assert.Equal((17, PartyFocus.List), (menu.Inventory.Cursor, menu.Focus));
        // The line below is short of the column, so onto its last row; then off the grid's foot.
        window.MoveCursor(1);
        Assert.Equal((19, PartyFocus.List), (menu.Inventory.Cursor, menu.Focus));
        window.MoveCursor(1);
        Assert.Equal((19, PartyFocus.Foot), (menu.Inventory.Cursor, menu.Focus));
        window.MoveCursor(-1);
        Assert.Equal(PartyFocus.List, menu.Focus);

        menu.Point(6);
        window.Sideways(-1);
        Assert.Equal(PartyFocus.Slots, menu.Focus);
        Assert.NotNull(menu.SelectedSlot);
        menu.Point(3);
        window.MoveCursor(-1);
        Assert.Equal((PartyFocus.Tabs, PartyList.Inventory), (menu.Focus, menu.List));
    }

    /// <summary>
    /// The grid holds only what's carried, the silhouette being where what's worn is reached; its Equipped filter shows what's worn, the
    /// sword's cell the wearer's, whatever the list's setting for what's worn.
    /// </summary>
    [Fact]
    public void WornItemsShowInTheGridOnlyUnderTheEquippedFilter()
    {
        var (menu, _) = Menu(showWorn: true);
        Assert.Equal(20, menu.Inventory.Rows.Count);
        Assert.All(menu.Inventory.Rows, r => Assert.Null(r.Wearer));

        menu.CycleFilter(-1);
        Assert.Equal(InventoryFilter.Equipped, menu.Filter);
        Assert.Equal([(Sword.Id, (int?)0)], menu.Inventory.Rows.Select(r => (r.Item.Id, r.Wearer)));
    }

    /// <summary>
    /// The filter shows what's carried of a kind and the order sorts it, A with the stick on either stepping it on, a click too; the grid
    /// starts again at its first item.
    /// </summary>
    [Fact]
    public void TheFilterAndOrderUnderTheGridStepOn()
    {
        var sword = ItemData.NewWeapon(2, WeaponClass.Sword) with { Name = "Zweihander", Price = 10 };
        var dagger = ItemData.NewWeapon(3, WeaponClass.Sword) with { Name = "Dagger", Price = 500 };
        var member = PotionData.Member("Alaron", 0, null);
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(40), [sword, dagger, .. Things]), null, [member]);
        foreach (var id in new[] { sword.Id, dagger.Id }.Concat(Things.Select(t => t.Id)))
            Assert.True(party.Inventory.Add(id));
        var menu = new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true };
        IPartyWindow window = menu;
        menu.Point(21);
        window.MoveCursor(1);
        Assert.Equal((PartyFocus.Foot, 0), (menu.Focus, menu.FootChoice));

        menu.Accept();
        Assert.Equal(InventoryFilter.Weapons, menu.Filter);
        Assert.Equal(["Dagger", "Zweihander"], menu.Inventory.Rows.Select(r => r.Item.Name));
        window.Sideways(1);
        menu.Accept();
        menu.Accept();
        Assert.Equal(InventorySort.Value, menu.Sort);
        Assert.Equal(["Dagger", "Zweihander"], menu.Inventory.Rows.Select(r => r.Item.Name));
        menu.ChooseFoot(1);
        Assert.Equal((InventorySort.Type, 0), (menu.Sort, menu.Inventory.Cursor));
    }

    /// <summary>
    /// In the original's look a click on a slot of the silhouette selects what's worn there, and A, or a double click, takes it off, as A
    /// on its row would; a slot with nothing in it isn't selected.
    /// </summary>
    [Fact]
    public void InTheOriginalsLookASelectedSlotsItemIsWhatATakesOff()
    {
        var (menu, member) = Menu(showWorn: false);
        menu.OriginalLook = true;
        menu.SelectSlot(EquipmentSlot.Helmet);
        Assert.Null(menu.SelectedSlot);

        menu.SelectSlot(EquipmentSlot.Weapon);
        Assert.Equal(Sword.Name, menu.Details()?.Title);
        menu.Accept();
        Assert.Null(member.Equipment.Weapon);
        Assert.Null(menu.SelectedSlot);
    }

    /// <summary>
    /// In the port's look a slot of the silhouette is selected empty or not, the stick going onto it, and the menus' Appraise takes off
    /// what's in it, the slot staying selected; the stick off the silhouette lets the slot go, A then being the cursor's row's again.
    /// </summary>
    [Fact]
    public void AppraiseOnASelectedSlotTakesItsItemOff()
    {
        var (menu, member) = Menu(showWorn: false);
        menu.SelectSlot(EquipmentSlot.Helmet);
        Assert.Equal(EquipmentSlot.Helmet, menu.SelectedSlot);
        Assert.False(((IPartyWindow)menu).Appraise());

        menu.SelectSlot(EquipmentSlot.Weapon);
        Assert.Equal(EquipmentSlot.Weapon, menu.SelectedSlot);
        Assert.Equal(Sword.Id, member.Equipment.Weapon?.Id);
        Assert.Equal(Sword.Name, menu.Details()?.Title);
        Assert.True(((IPartyWindow)menu).Appraise());
        Assert.Null(member.Equipment.Weapon);
        Assert.Equal(EquipmentSlot.Weapon, menu.SelectedSlot);
        menu.Back();
        Assert.Contains(menu.Inventory.Rows, r => r.Item.Id == Sword.Id && r.Wearer is null);

        Assert.Equal(EquipRefusal.None, menu.Party.Equip(member, Sword.Id));
        menu.Refresh();
        menu.SelectSlot(EquipmentSlot.Scroll2);
        ((IPartyWindow)menu).Sideways(1);
        Assert.Null(menu.SelectedSlot);
        Assert.Equal(PartyFocus.List, menu.Focus);
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
        Assert.True(((IPartyWindow)menu).Appraise());

        Assert.Equal(Sword.Id, member.Equipment.Weapon?.Id);
        Assert.Equal(PartyMenu.NoRoom, menu.Message?.Text);
    }

    /// <summary>
    /// The item box says which buttons do what with the selected item, named on the device in use: Change and Unequip on a worn slot with
    /// the details, A's action on a carried item, and nothing for a plain item with no details; none with the mouse in use, nor in the
    /// original's look.
    /// </summary>
    [Fact]
    public void TheItemBoxSaysWhichButtonsDoWhat()
    {
        var (menu, member) = Menu(showWorn: false);
        menu.KeyName = Keys;
        menu.Point(menu.Inventory.Rows.ToList().FindIndex(r => r.Item.Id == Things[0].Id));
        Assert.NotNull(menu.BoxShown);
        Assert.Empty(menu.BoxShown!.Value.Help);

        menu.SelectSlot(EquipmentSlot.Weapon);
        Assert.Equal([new MenuPrompt("A", "Change"), new MenuPrompt("X", "Unequip"), new MenuPrompt("Y", "See Details")], menu.BoxShown!.Value.Help);
        Assert.True(menu.Party.Unequip(member, member.Equipment.Weapon!));
        menu.Refresh();
        menu.Back();
        menu.Point(menu.Inventory.Rows.ToList().FindIndex(r => r.Item.Id == Sword.Id));
        Assert.Equal([new MenuPrompt("A", "Equip"), new MenuPrompt("Y", "See Details")], menu.BoxShown!.Value.Help);

        menu.Pointing = true;
        Assert.Null(menu.BoxShown);
        menu.Pointing = false;
        menu.OriginalLook = true;
        Assert.Null(menu.BoxShown);
    }

    /// <summary>
    /// C-down's button holds the details open in the port's look, and any press lets them go, the stick's among them, which moves nothing
    /// while they fade.
    /// </summary>
    [Fact]
    public void AnyPressLetsTheHeldDetailsGo()
    {
        var (menu, member) = Menu(showWorn: false);
        Assert.True(menu.Party.Unequip(member, member.Equipment.Weapon!));
        menu.Refresh();
        var sword = menu.Inventory.Rows.ToList().FindIndex(r => r.Item.Id == Sword.Id);
        menu.Point(sword);
        menu.PressDetails();
        Assert.True(menu.DetailsPane is { Showing: true, Pinned: true });
        menu.Step();
        menu.MoveCursor(1);
        Assert.True(menu.DetailsPane is { Showing: true, Pinned: false });
        Assert.Equal(sword, menu.Inventory.Cursor);
        for (var i = 0; i < 120 && menu.DetailsPane.Showing; i++)
            menu.Step();
        Assert.False(menu.DetailsPane.Showing);

        menu.PressDetails();
        Assert.True(menu.DetailsPane.Pinned);
        ((IPartyWindow)menu).Sideways(1);
        Assert.False(menu.DetailsPane.Pinned);
        Assert.Equal(PartyList.Inventory, menu.List);
    }

    /// <summary>
    /// A press the mouse makes is never taken just to show the hidden cursor: a click on a tab aims the stick's press, and a double click
    /// moves the cursor quietly before its A; a key's press after them is taken as ever.
    /// </summary>
    [Fact]
    public void AClicksPressIsNeverTakenToShowTheCursor()
    {
        var (menu, _) = Menu(showWorn: false);
        menu.Pointing = true;

        menu.AimAt(PartyList.Stats);
        Assert.False(menu.ShowCursor(MenuAction.Right));
        menu.AimAt(null);
        menu.Point(3, quietly: true);
        Assert.False(menu.ShowCursor(MenuAction.Accept));
        Assert.True(menu.ShowCursor(MenuAction.Accept));
    }

    /// <summary>
    /// The trash under the grid starts choosing items to drop: A on a cell, or a click, chooses it, and Appraise, or Shift and a click,
    /// lets it go, any number; B, the trash again with nothing chosen, or the list changing ends it, letting them all go. What can't be
    /// dropped isn't chosen.
    /// </summary>
    [Fact]
    public void TheTrashUnderTheGridChoosesItemsToDrop()
    {
        var (menu, _) = Menu(showWorn: false);
        menu.KeyName = Keys;
        menu.ChooseFoot(PartyMenu.FootTrash);
        Assert.True(menu.Discarding);
        Assert.Equal(PartyFocus.List, menu.Focus);

        menu.Point(2);
        menu.Accept();
        Assert.True(menu.IsDiscard(menu.Inventory.Rows[2]));
        Assert.Equal(1, menu.DiscardCount(menu.Inventory.Rows[2]));
        Assert.Contains(new MenuPrompt("X", "Unselect"), menu.Prompts);
        Assert.DoesNotContain(new MenuPrompt("A", "Select"), menu.Prompts);
        Assert.Contains(new MenuPrompt("B", "Done", Cancels: true), menu.Prompts);
        // One of a lone item is the whole of it: A again chooses no more.
        menu.Accept();
        Assert.Equal(1, menu.DiscardCount(menu.Inventory.Rows[2]));
        Assert.True(menu.AddDiscard(menu.Inventory.Rows[3]));
        Assert.Equal(2, menu.Discards.Count);
        Assert.True(((IPartyWindow)menu).Appraise());
        Assert.False(menu.IsDiscard(menu.Inventory.Rows[2]));
        Assert.False(menu.RemoveDiscard(menu.Inventory.Rows[2]));
        Assert.True(menu.Back());
        Assert.False(menu.Discarding);
        Assert.Empty(menu.Discards);

        menu.ChooseFoot(PartyMenu.FootTrash);
        menu.ChangeList(1);
        Assert.False(menu.Discarding);
        menu.ShowList(PartyList.Inventory);
        menu.ChooseFoot(PartyMenu.FootTrash);
        menu.ChooseFoot(PartyMenu.FootTrash);
        Assert.False(menu.Discarding);
    }

    /// <summary>
    /// A stack is chosen a unit at a time, up to the whole stack, and let go a unit at a time; the trash with items chosen asks whether to
    /// drop them, naming one item, some of one stack, some of one kind or some items, with the cursor on No; B or No takes the question
    /// down with nothing dropped, and Yes drops them and ends choosing.
    /// </summary>
    [Fact]
    public void TheTrashAsksBeforeItDropsWhatIsChosen()
    {
        // Three potions, stacked, among three of the things.
        var potion = ItemData.NewItem(60, ItemKind.Potion) with { Name = "Healing Potion" };
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(40), [Sword, potion, .. Things]), null, [PotionData.Member("Alaron", 0, null)]);
        foreach (var thing in Things.Take(3))
            Assert.True(party.Inventory.Add(thing.Id));
        Assert.True(party.Inventory.Add(potion.Id, 3));
        var menu = new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true, KeyName = Keys };
        menu.ChooseFoot(PartyMenu.FootTrash);
        var potions = menu.Inventory.Rows.Single(r => r.Item.Id == potion.Id);
        menu.Point(RowOf(potion.Id));

        menu.Accept();
        Assert.Equal("Are you sure you want to drop Healing Potion?", menu.DropQuestion());
        Assert.Contains(new MenuPrompt("A", "Select One More"), menu.Prompts);
        menu.Accept();
        Assert.Equal(2, menu.DiscardCount(potions));
        Assert.Equal("Are you sure you want to drop 2 Healing Potions?", menu.DropQuestion());
        Assert.Contains(new MenuPrompt("X", "Unselect One"), menu.Prompts);
        menu.Accept();
        menu.Accept();
        Assert.Equal(3, menu.DiscardCount(potions));
        Assert.True(menu.RemoveDiscard(potions));
        Assert.Equal(2, menu.DiscardCount(potions));

        // Two of the things with them: a mix of kinds; the things alone, all of one kind.
        Assert.True(menu.AddDiscard(menu.Inventory.Rows[RowOf(Things[0].Id)]));
        Assert.True(menu.AddDiscard(menu.Inventory.Rows[RowOf(Things[1].Id)]));
        Assert.Equal("Are you sure you want to drop 4 items?", menu.DropQuestion());
        Assert.True(menu.RemoveDiscard(potions));
        Assert.True(menu.RemoveDiscard(potions));
        Assert.Equal("Are you sure you want to drop 2 items?", menu.DropQuestion());

        // The trash asks; B takes the question down, and the choice stays.
        menu.ChooseFoot(PartyMenu.FootTrash);
        Assert.NotNull(menu.DropChoice);
        Assert.Equal(1, menu.DropChoice!.Choices.Cursor);
        Assert.Contains(new MenuPrompt("B", "Cancel", Cancels: true), menu.Prompts);
        Assert.True(menu.Back());
        Assert.Null(menu.DropChoice);
        Assert.True(menu.Discarding);
        Assert.Equal(2, menu.Discards.Count);
        // No, chosen, does the same.
        menu.ChooseFoot(PartyMenu.FootTrash);
        menu.Accept();
        Assert.Null(menu.DropChoice);
        Assert.Equal(2, menu.Discards.Count);
        // Yes drops them.
        menu.ChooseFoot(PartyMenu.FootTrash);
        menu.MoveCursor(-1);
        Assert.Equal(0, menu.DropChoice!.Choices.Cursor);
        menu.Accept();
        Assert.Null(menu.DropChoice);
        Assert.False(menu.Discarding);
        Assert.False(party.Inventory.Has(Things[0].Id));
        Assert.False(party.Inventory.Has(Things[1].Id));
        Assert.Equal(3, party.Inventory.Count(potion.Id));
        Assert.Equal(2, menu.Inventory.Rows.Count);

        // Some of a stack leaves the rest.
        menu.ChooseFoot(PartyMenu.FootTrash);
        menu.Point(RowOf(potion.Id));
        menu.Accept();
        menu.Accept();
        menu.ChooseFoot(PartyMenu.FootTrash);
        menu.MoveCursor(-1);
        menu.Accept();
        Assert.Equal(1, party.Inventory.Count(potion.Id));

        int RowOf(RecordId id) => menu.Inventory.Rows.Select((r, i) => (r, i)).First(p => p.r.Item.Id == id).i;
    }

    /// <summary>An item's name for more than one: most take an s, some es or ies, and armor, mail and food by the piece stay as they are.</summary>
    [Theory]
    [InlineData("Healing Potion", "Healing Potions")]
    [InlineData("Torch", "Torches")]
    [InlineData("Berry", "Berries")]
    [InlineData("Key", "Keys")]
    [InlineData("Leather Armor", "Leather Armor")]
    [InlineData("Chain Mail", "Chain Mail")]
    [InlineData("Fish", "Fish")]
    public void AnItemsNameHasAPlural(string name, string plural) => Assert.Equal(plural, PartyMenu.Plural(name));

    /// <summary>A controller's names for the menus' buttons, as the Controls page gives them.</summary>
    private static string? Keys(string control) => control switch
    {
        "menu_accept" => "A",
        "menu_back" => "B",
        "menu_details" => "Y",
        "menu_appraise" => "X",
        _ => null,
    };

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

    /// <summary>
    /// On the tabs the stick steps to the tab beside, the list changing with it and stopping at the ends, down or A goes into the list,
    /// and up from a list's first row comes back onto them; B takes the stick back onto the list.
    /// </summary>
    [Fact]
    public void TheStickStepsAlongTheTabsAndBackDown()
    {
        var (menu, _) = Menu(showWorn: false);
        IPartyWindow window = menu;
        window.MoveCursor(-1);
        Assert.Equal(PartyFocus.Tabs, menu.Focus);

        window.Sideways(-1);
        Assert.Equal(PartyList.Inventory, menu.List);
        window.Sideways(1);
        Assert.Equal((PartyList.Stats, PartyFocus.Tabs), (menu.List, menu.Focus));
        window.MoveCursor(1);
        Assert.Equal(PartyFocus.List, menu.Focus);
        window.MoveCursor(-1);
        Assert.Equal(PartyFocus.Tabs, menu.Focus);
        menu.Accept();
        Assert.Equal(PartyFocus.List, menu.Focus);

        window.MoveCursor(-1);
        Assert.True(menu.Back());
        Assert.Equal(PartyFocus.List, menu.Focus);
        Assert.False(menu.Back());
    }

    /// <summary>
    /// On the silhouette the stick steps to the nearest slot its way, empty or not, and right off the silhouette's right column back onto
    /// the grid; B takes it back onto the grid too.
    /// </summary>
    [Fact]
    public void TheStickStepsBetweenTheSlotsByWhereTheyAre()
    {
        var (menu, _) = Menu(showWorn: false);
        IPartyWindow window = menu;
        menu.SelectSlot(EquipmentSlot.Helmet);

        window.MoveCursor(1);
        Assert.Equal(EquipmentSlot.Amulet, menu.SelectedSlot);
        window.MoveCursor(-1);
        window.MoveCursor(-1);
        Assert.Equal(EquipmentSlot.Helmet, menu.SelectedSlot);
        menu.SelectSlot(EquipmentSlot.Weapon);
        window.Sideways(1);
        Assert.Equal(EquipmentSlot.Cloak, menu.SelectedSlot);
        menu.SelectSlot(EquipmentSlot.Scroll1);
        window.Sideways(1);
        Assert.Equal(PartyFocus.List, menu.Focus);
        menu.SelectSlot(EquipmentSlot.Boots);
        Assert.True(menu.Back());
        Assert.Equal(PartyFocus.List, menu.Focus);
    }

    /// <summary>
    /// The prompts along the foot name each button on the device in use and what it does with what's selected: A's action for the item,
    /// Appraise's Unequip on a worn slot, the details, the member and Back, but none for what an item hasn't, as a thing of no use with
    /// no description; none in the original's look.
    /// </summary>
    [Fact]
    public void ThePromptsSayWhatEachButtonDoes()
    {
        var (menu, _) = Menu(showWorn: false);
        menu.KeyName = Keys;

        Assert.Equal([new MenuPrompt("B", "Close", Cancels: true)], menu.Prompts);
        menu.SelectSlot(EquipmentSlot.Weapon);
        Assert.Equal([new MenuPrompt("A", "Change"), new MenuPrompt("X", "Unequip"), new MenuPrompt("Y", "Details"), new MenuPrompt("B", "Back", Cancels: true)],
            menu.Prompts);
        menu.OriginalLook = true;
        Assert.Empty(menu.Prompts);
    }

    /// <summary>
    /// The cancel's seal shows with the mouse in use wherever a prompt is B's, Close here, and not with a key or a controller, which have
    /// B, nor in the original's look (the user's design, Nov 6, 2026).
    /// </summary>
    [Fact]
    public void TheCancelSealShowsWithTheMouseWhereBCancels()
    {
        var (menu, _) = Menu(showWorn: false);
        menu.KeyName = Keys;

        Assert.False(menu.CancelShows);
        menu.Pointing = true;
        Assert.True(menu.CancelShows);
        Assert.Contains(menu.Prompts, p => p.Cancels && p.Label == "Close");
        menu.OriginalLook = true;
        Assert.False(menu.CancelShows);
    }
}
