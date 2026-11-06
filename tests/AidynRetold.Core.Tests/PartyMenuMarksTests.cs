using AidynRetold.Core.Items;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>The grid's marks for the member shown, the card over an item, and C-down's button turning the details on and off.</summary>
public class PartyMenuMarksTests
{
    private static readonly WeaponRecord Heavy = ItemData.NewWeapon(1, strength: 99);

    private static readonly ArmorRecord Old = ItemData.NewArmor(1, protection: 2), Better = ItemData.NewArmor(2, protection: 5),
        Worse = ItemData.NewArmor(3, protection: 1);

    private static PartyMenu Menu()
    {
        var member = PotionData.Member("Alaron", 0, null);
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(40), [Heavy, Old, Better, Worse]), null, [member]);
        foreach (var id in new[] { Heavy.Id, Old.Id, Better.Id, Worse.Id })
            Assert.True(party.Inventory.Add(id));
        Assert.Equal(EquipRefusal.None, party.Equip(member, Old.Id));
        return new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true, ShowWorn = false };
    }

    private static InventoryRow Row(PartyMenu menu, RecordId id) => menu.Inventory.Rows.Single(r => r.Item.Id == id);

    /// <summary>
    /// What the member can't put on is marked unusable and its card says why; what would be better than what they wear is marked so
    /// and its card says than what; what's no better is neither.
    /// </summary>
    [Fact]
    public void TheGridMarksWhatTheMemberCantUseAndWhatsBetter()
    {
        var menu = Menu();

        Assert.Equal(ItemMark.Unusable, menu.Mark(Row(menu, Heavy.Id)));
        Assert.Equal("Needs 99 Strength", menu.Card(Row(menu, Heavy.Id)).Warning);
        // The arrow shows only while the item is selected, or a slot it fits is (the user's choice, Nov 5, 2026).
        Assert.Equal(ItemMark.None, menu.Mark(Row(menu, Better.Id)));
        Assert.Equal(ItemMark.Upgrade, menu.Mark(Row(menu, Better.Id), selected: true));
        Assert.Equal(new ItemCard(Better.Name, "Protection 5", null, $"Better than {Old.Name}"), menu.Card(Row(menu, Better.Id)));
        Assert.Equal(ItemMark.None, menu.Mark(Row(menu, Worse.Id)));
        Assert.Equal(new ItemCard(Worse.Name, "Protection 1"), menu.Card(Row(menu, Worse.Id)));
    }

    /// <summary>
    /// In the port's look C-down's button turns the details on, held open as the actiontip's are, through its letting go, and off again;
    /// the original's shows them while it's held.
    /// </summary>
    [Fact]
    public void TheDetailsButtonTurnsThemOnAndOff()
    {
        var menu = Menu();
        IPartyWindow window = menu;
        menu.Point(0);

        window.Press(MenuAction.Details);
        window.Press(MenuAction.DetailsRelease);
        Assert.True(menu.DetailsPane.Pinned);
        window.Press(MenuAction.Details);
        Assert.False(menu.DetailsPane.Pinned);

        menu.OriginalLook = true;
        for (var tick = 0; tick < 60; tick++)
            menu.Step();
        window.Press(MenuAction.Details);
        Assert.True(menu.DetailsPane.Held);
        window.Press(MenuAction.DetailsRelease);
        Assert.False(menu.DetailsPane.Held);
    }
}
