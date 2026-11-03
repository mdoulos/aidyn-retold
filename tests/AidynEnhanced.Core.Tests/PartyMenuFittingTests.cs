using AidynEnhanced.Core.Characters;
using AidynEnhanced.Core.Items;
using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Tests;

/// <summary>The silhouette as the place to equip, in the port's look: a slot shows what fits it, and A chooses what goes in it.</summary>
public class PartyMenuFittingTests
{
    private static readonly ItemRecord Cap = ItemData.NewItem(1, ItemKind.Helmet);

    private static readonly ItemRecord Crown = ItemData.NewItem(2, ItemKind.Helmet,
        bonuses: new GearBonuses(Stat: new StatBonus(StatId.Record(Stat.Strength), 2)));

    private static readonly ItemRecord[] Rings = [.. Enumerable.Range(3, 3).Select(n => ItemData.NewItem(n, ItemKind.Ring))];

    private static readonly ItemRecord[] Things = [.. Enumerable.Range(10, 5).Select(n => ItemData.NewItem(n))];

    /// <summary>Alaron carrying two helmets, three rings and five things, on the tab's grid.</summary>
    private static (PartyMenu Menu, Character Member) Menu()
    {
        var member = PotionData.Member("Alaron", 0, null);
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(40), [Cap, Crown, .. Rings, .. Things]), null, [member]);
        foreach (var item in new[] { Cap, Crown }.Concat(Rings).Concat(Things))
            Assert.True(party.Inventory.Add(item.Id));
        return (new PartyMenu(party, new MenuText([]), _ => null, _ => null) { Grid = true, ShowWorn = false }, member);
    }

    /// <summary>
    /// A slot selected shows on the grid only what fits it, starting at its first item; leaving the silhouette shows the whole inventory
    /// again with the cursor where it was.
    /// </summary>
    [Fact]
    public void ASlotShowsOnTheGridWhatFitsIt()
    {
        var (menu, _) = Menu();
        menu.Point(6);

        menu.SelectSlot(EquipmentSlot.Helmet);
        Assert.Equal(EquipmentSlot.Helmet, menu.Fitting);
        Assert.Equal([Cap.Id, Crown.Id], menu.Inventory.Rows.Select(r => r.Item.Id));
        menu.SelectSlot(EquipmentSlot.Ring2);
        Assert.Equal(Rings.Select(r => r.Id), menu.Inventory.Rows.Select(r => r.Item.Id));

        Assert.True(menu.Back());
        Assert.Null(menu.Fitting);
        Assert.Equal(10, menu.Inventory.Rows.Count);
        Assert.Equal(6, menu.Inventory.Cursor);
    }

    /// <summary>
    /// A on a slot goes onto the grid of what fits it, where the stats show what an item would change, and A on one puts it on in that
    /// slot, the stick going back to the slot; B there goes back to the slot without putting anything on.
    /// </summary>
    [Fact]
    public void AOnASlotChoosesWhatGoesInIt()
    {
        var (menu, member) = Menu();
        menu.SelectSlot(EquipmentSlot.Helmet);
        menu.Accept();
        Assert.Equal((EquipmentSlot.Helmet, PartyFocus.List), (menu.Picking, menu.Focus));

        Assert.True(menu.Back());
        Assert.Equal((EquipmentSlot.Helmet, (EquipmentSlot?)null), (menu.SelectedSlot, menu.Picking));
        menu.Accept();

        menu.Point(1);
        Assert.Equal(EquipmentSlot.Helmet, menu.Picking);
        Assert.Equal(new Dictionary<Stat, int> { [Stat.Strength] = 2 }, menu.CursorStatChanges);
        var strength = member.Stats.Modified(Stat.Strength);
        menu.Accept();
        Assert.Equal(Crown.Id, menu.Worn(EquipmentSlot.Helmet)?.Id);
        Assert.Equal(strength + 2, member.Stats.Modified(Stat.Strength));
        Assert.Equal((EquipmentSlot.Helmet, (EquipmentSlot?)null), (menu.SelectedSlot, menu.Picking));
        Assert.Equal([Cap.Id], menu.Inventory.Rows.Select(r => r.Item.Id));
    }

    /// <summary>A click on the grid of what fits the slot selected chooses for it, as A on the slot would.</summary>
    [Fact]
    public void AClickOnWhatFitsChoosesForTheSlot()
    {
        var (menu, _) = Menu();
        menu.SelectSlot(EquipmentSlot.Helmet);

        menu.Point(0);
        Assert.Equal((EquipmentSlot.Helmet, (EquipmentSlot?)null), (menu.Picking, menu.SelectedSlot));
        menu.Accept();
        Assert.Equal(Cap.Id, menu.Worn(EquipmentSlot.Helmet)?.Id);
    }

    /// <summary>A ring chosen for the second ring's slot takes that ring's place, where rings are otherwise added, two at most.</summary>
    [Fact]
    public void ARingChosenForASlotTakesThatRingsPlace()
    {
        var (menu, member) = Menu();
        Assert.Equal(EquipRefusal.None, menu.Party.Equip(member, Rings[0].Id));
        Assert.Equal(EquipRefusal.None, menu.Party.Equip(member, Rings[1].Id));
        menu.Refresh();

        menu.SelectSlot(EquipmentSlot.Ring2);
        menu.Accept();
        menu.Point(menu.Inventory.Rows.Select((r, i) => (r, i)).Single(e => e.r.Item.Id == Rings[2].Id).i);
        menu.Accept();

        Assert.Equal([Rings[0].Id, Rings[2].Id], member.Equipment.Gear.OfType<ItemInstance>().Select(g => g.Id));
        Assert.NotNull(menu.Party.Inventory.Find(Rings[1].Id));
        Assert.Null(menu.Message);
    }

    /// <summary>
    /// What putting an item on would change: its stat bonus less that of what it replaces, the helmet worn, nothing where they're the
    /// same, and nothing for what isn't put on.
    /// </summary>
    [Fact]
    public void StatChangesAreTheBonusLessWhatsReplaced()
    {
        var (menu, member) = Menu();
        Assert.Equal(new Dictionary<Stat, int> { [Stat.Strength] = 2 }, menu.StatChanges(menu.Party.Inventory.Catalog[Crown.Id]));
        Assert.Equal(EquipRefusal.None, menu.Party.Equip(member, Crown.Id));
        Assert.Equal(new Dictionary<Stat, int> { [Stat.Strength] = -2 }, menu.StatChanges(menu.Party.Inventory.Catalog[Cap.Id]));
        Assert.Empty(menu.StatChanges(menu.Party.Inventory.Catalog[Things[0].Id]));
    }
}
