using AidynRetold.Core.Items;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

public class ItemPictureTests
{
    private static Item Potion(int id, RecordId icon) =>
        Item.From(new ItemRecord(RecordId.Hex(RecordTypes.Item, id), "Potion", icon, 0, ItemKind.Potion));

    private static Item Scroll(SpellRecord? spell, RecordId? icon = null) =>
        Item.From(new ItemRecord(RecordId.Hex(RecordTypes.Item, 0x1101), "Scroll", icon ?? MenuImages.Scroll, 0, ItemKind.Scroll,
            Bonuses: spell is null ? null : new GearBonuses(Spell: new ItemSpell(spell.Id, 1, 1))));

    private static readonly Func<RecordId, SpellRecord?> NoSpells = _ => null;

    /// <summary>Each potion and flask the original draws with the one flask is drawn with its own picture, which the views carry.</summary>
    [Theory]
    [InlineData(0x1000, "acre:image/potion_fire")]
    [InlineData(0x1002, "acre:image/potion_sleep_gas")]
    [InlineData(0x1004, "acre:image/potion_healing")]
    [InlineData(0x1010, "acre:image/potion_stealth")]
    [InlineData(0x100D, "acre:image/potion_literacy")]
    public void APotionIsDrawnWithItsOwnPicture(int id, string picture)
    {
        Assert.Equal(RecordId.Parse(picture), MenuImages.ItemIcon(Potion(id, MenuImages.Potion), NoSpells));
    }

    /// <summary>The ACRE plugin's own potions, Intellect, Determination and Ingenuity, have theirs too.</summary>
    [Theory]
    [InlineData("acre:item/0x1011", "acre:image/potion_intellect")]
    [InlineData("acre:item/0x1012", "acre:image/potion_determination")]
    [InlineData("acre:item/0x1013", "acre:image/potion_ingenuity")]
    public void TheRemadeGamesPotionsAreDrawnWithTheirOwn(string id, string picture)
    {
        var potion = Item.From(new ItemRecord(RecordId.Parse(id), "Potion", MenuImages.Potion, 0, ItemKind.Potion));
        Assert.Equal(RecordId.Parse(picture), MenuImages.ItemIcon(potion, NoSpells));
        Assert.Equal(MenuImages.Potion, MenuImages.Shared(RecordId.Parse(picture)));
    }

    /// <summary>Fifteen of the original's have one, and the ACRE plugin's three: the cut A and J keep the flask.</summary>
    [Fact]
    public void TheCutPotionsKeepTheFlask()
    {
        Assert.Equal(18, MenuImages.PotionPictures.Count);
        Assert.Equal(MenuImages.Potion, MenuImages.ItemIcon(Potion(0x100B, MenuImages.Potion), NoSpells));
        Assert.Equal(MenuImages.Potion, MenuImages.ItemIcon(Potion(0x100C, MenuImages.Potion), NoSpells));
    }

    /// <summary>A mod that gives a potion or a scroll an icon of its own has it drawn.</summary>
    [Fact]
    public void AModsIconIsDrawn()
    {
        var icon = RecordId.Parse("mymod:image/green_flask");
        Assert.Equal(icon, MenuImages.ItemIcon(Potion(0x1004, icon), NoSpells));
        Assert.Equal(icon, MenuImages.ItemIcon(Scroll(Characters.Fireball, icon), _ => Characters.Fireball));
    }

    /// <summary>A scroll is drawn with its spell's school's picture; one of no school, or with no spell, with the white one.</summary>
    [Theory]
    [InlineData(MagicSchool.Elemental, "acre:image/scroll_elemental")]
    [InlineData(MagicSchool.Naming, "acre:image/scroll_naming")]
    [InlineData(MagicSchool.Necromancy, "acre:image/scroll_necromancy")]
    [InlineData(MagicSchool.Star, "acre:image/scroll_star")]
    [InlineData(MagicSchool.None, "acre:image/scroll_neutral")]
    public void AScrollIsDrawnByItsSpellsSchool(MagicSchool school, string picture)
    {
        var spell = Characters.Spell(0x0300, school, wizard: 1, cost: 100);
        Assert.Equal(RecordId.Parse(picture), MenuImages.ItemIcon(Scroll(spell), id => id == spell.Id ? spell : null));
        Assert.Equal(MenuImages.ScrollPictures[MagicSchool.None], MenuImages.ItemIcon(Scroll(null), NoSpells));
    }

    /// <summary>No spell is Chaos's, so a Chaos scroll, a mod's, keeps the original's.</summary>
    [Fact]
    public void AChaosScrollKeepsTheOriginals()
    {
        var spell = Characters.Spell(0x0300, MagicSchool.Chaos, wizard: 1, cost: 100);
        Assert.Equal(MenuImages.Scroll, MenuImages.ItemIcon(Scroll(spell), _ => spell));
    }

    /// <summary>In the original look each picture draws the flask or scroll it stands in for; no other image stands in for one.</summary>
    [Fact]
    public void APictureStandsInForTheOriginals()
    {
        Assert.All(MenuImages.PotionPictures.Values, p => Assert.Equal(MenuImages.Potion, MenuImages.Shared(p)));
        Assert.All(MenuImages.ScrollPictures.Values, p => Assert.Equal(MenuImages.Scroll, MenuImages.Shared(p)));
        Assert.Null(MenuImages.Shared(MenuImages.Potion));
        Assert.Null(MenuImages.Shared(RecordId.Parse("acre:image/potion_unknown")));
    }
}
