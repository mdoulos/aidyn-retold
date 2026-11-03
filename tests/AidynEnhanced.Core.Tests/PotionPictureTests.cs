using AidynEnhanced.Core.Items;
using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Tests;

public class PotionPictureTests
{
    private static Item Potion(int id, RecordId icon) =>
        Item.From(new ItemRecord(RecordId.Hex(RecordTypes.Item, id), "Potion", icon, 0, ItemKind.Potion));

    /// <summary>Each potion and flask the original draws with the one flask is drawn with its own picture, which the views carry.</summary>
    [Theory]
    [InlineData(0x1000, "aidyn_enhanced:image/potion_fire")]
    [InlineData(0x1002, "aidyn_enhanced:image/potion_sleep_gas")]
    [InlineData(0x1004, "aidyn_enhanced:image/potion_healing")]
    [InlineData(0x1010, "aidyn_enhanced:image/potion_stealth")]
    public void APotionIsDrawnWithItsOwnPicture(int id, string picture)
    {
        Assert.Equal(RecordId.Parse(picture), MenuImages.ItemIcon(Potion(id, MenuImages.Potion)));
    }

    /// <summary>Fifteen have one: the cut A and J keep the flask.</summary>
    [Fact]
    public void TheCutPotionsKeepTheFlask()
    {
        Assert.Equal(15, MenuImages.PotionPictures.Count);
        Assert.Equal(MenuImages.Potion, MenuImages.ItemIcon(Potion(0x100B, MenuImages.Potion)));
        Assert.Equal(MenuImages.Potion, MenuImages.ItemIcon(Potion(0x100C, MenuImages.Potion)));
    }

    /// <summary>A mod that gives a potion an icon of its own has it drawn.</summary>
    [Fact]
    public void AModsIconForAPotionIsDrawn()
    {
        var icon = RecordId.Parse("mymod:image/green_flask");
        Assert.Equal(icon, MenuImages.ItemIcon(Potion(0x1004, icon)));
    }

    /// <summary>In the original look each picture draws the flask it stands in for; no other image stands in for one.</summary>
    [Fact]
    public void APictureStandsInForTheFlask()
    {
        Assert.All(MenuImages.PotionPictures.Values, p => Assert.Equal(MenuImages.Potion, MenuImages.Shared(p)));
        Assert.Null(MenuImages.Shared(MenuImages.Potion));
        Assert.Null(MenuImages.Shared(RecordId.Parse("aidyn_enhanced:image/potion_unknown")));
    }
}
