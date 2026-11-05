using AidynRetold.Core.Calendar;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Flags;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using CharacterParty = AidynRetold.Core.Characters.Party;

namespace AidynRetold.Core.Tests;

/// <summary>The remade game's spoilage (the user's design, Nov 5, 2026): food that keeps for so many days, the oldest of a stack going first.</summary>
public class SpoilageTests
{
    private const ulong Hour = GameClock.Hour, Day = GameClock.Day;

    /// <summary>A snack that keeps a week.</summary>
    private static readonly ItemRecord Apple = ItemData.NewItem(0x2101, ItemKind.Food) with { Name = "Apple", Satiety = 1, Keeps = 7 };

    /// <summary>A dish that keeps two days.</summary>
    private static readonly ItemRecord Stew = ItemData.NewItem(0x2102, ItemKind.Food) with { Name = "Meat Stew", Satiety = 3, Keeps = 2 };

    /// <summary>An ingredient that keeps for good.</summary>
    private static readonly ItemRecord Oats = ItemData.NewItem(0x2103, ItemKind.Misc) with { Name = "Bag of Oats" };

    private static ItemCatalog Catalog() => ItemData.Catalog(Apple, Stew, Oats);

    private static (CharacterParty Party, ItemStack Apples, ItemStack Stews, ItemStack Oats) Pantry()
    {
        var party = new CharacterParty(Catalog());
        party.Inventory.Add(Apple.Id, 2);
        party.Inventory.Add(Stew.Id, 3);
        party.Inventory.Add(Oats.Id, 5);
        return (party, party.Inventory.Find(Apple.Id)!, party.Inventory.Find(Stew.Id)!, party.Inventory.Find(Oats.Id)!);
    }

    [Fact]
    public void AnItemKeepsForItsDaysAndOneWithNoneForGood()
    {
        var catalog = Catalog();
        Assert.Equal(7 * Day, catalog[Apple.Id].KeepsFor);
        Assert.True(catalog[Stew.Id].Spoils);
        Assert.False(catalog[Oats.Id].Spoils);
        Assert.Equal(0UL, catalog[Oats.Id].KeepsFor);
    }

    [Fact]
    public void UnitsComeFreshAndAgeTogetherTheOldestFirst()
    {
        var (party, apples, stews, oats) = Pantry();
        Assert.Equal([0UL, 0UL], apples.Ages);
        Assert.Empty(oats.Ages);
        Assert.Null(oats.Freshness);
        Assert.Equal(2 * Day, stews.Left);
        Assert.Equal(Freshness.Fresh, stews.Freshness);

        Assert.Empty(party.Inventory.Age(Day));
        party.Inventory.Add(Stew.Id);
        Assert.Equal([Day, Day, Day, 0UL], stews.Ages);
        Assert.Equal(Day, stews.Left);
        Assert.Equal(Freshness.Half, stews.Freshness);

        // The three old stews go at two days; the fresh one stays, and the apples have most of their week.
        var tossed = party.Inventory.Age(Day);
        Assert.Equal([new Tossed(stews.Item, 3)], tossed);
        Assert.Equal(1, party.Inventory.Count(Stew.Id));
        Assert.Equal([Day], stews.Ages);
        Assert.Equal(2, party.Inventory.Count(Apple.Id));
        Assert.Equal(Freshness.Fresh, apples.Freshness);
        Assert.Equal(5, party.Inventory.Count(Oats.Id));

        // The last stew goes too, and its stack with it.
        Assert.Equal([new Tossed(stews.Item, 1)], party.Inventory.Age(Day));
        Assert.Null(party.Inventory.Find(Stew.Id));
    }

    [Fact]
    public void TheClockTurnsYellowAtHalfAndRedWithinACampOfSpoiling()
    {
        var keeps = 2 * Day;
        Assert.Equal(Freshness.Fresh, Spoilage.Of(keeps, keeps));
        Assert.Equal(Freshness.Fresh, Spoilage.Of(Day + 1, keeps));
        Assert.Equal(Freshness.Half, Spoilage.Of(Day, keeps));
        Assert.Equal(Freshness.Half, Spoilage.Of(8 * Hour + 1, keeps));
        Assert.Equal(Freshness.Spoiling, Spoilage.Of(8 * Hour, keeps));
        Assert.Equal(Freshness.Spoiling, Spoilage.Of(0, keeps));
    }

    [Fact]
    public void TakingFromAStackTakesTheOldestSoTheClockGoesGreenAgain()
    {
        var (party, apples, _, _) = Pantry();
        party.Inventory.Age(6 * Day + 20 * Hour);
        party.Inventory.Add(Apple.Id, 3);
        Assert.Equal(Freshness.Spoiling, apples.Freshness);
        Assert.Equal(4 * Hour, apples.Left);

        // Two eaten: the two old ones.
        party.Inventory.Take(Apple.Id, 2);
        Assert.Equal([0UL, 0UL, 0UL], apples.Ages);
        Assert.Equal(Freshness.Fresh, apples.Freshness);
    }

    [Fact]
    public void AStackAtItsLimitKeepsItsOldUnitsAndLosesTheSurplus()
    {
        var (party, apples, _, _) = Pantry();
        party.Inventory.Age(Day);
        var limit = party.Inventory.Catalog.Rules.StackLimit;
        party.Inventory.Add(Apple.Id, limit);
        Assert.Equal(limit, apples.Quantity);
        Assert.Equal(limit, apples.Ages.Count);
        Assert.Equal([Day, Day], apples.Ages.Take(2));
        Assert.Equal(0UL, apples.Ages[^1]);
    }

    [Fact]
    public void CampingsHoursPassForTheFoodAndSayWhatSpoiled()
    {
        var (party, _, stews, _) = Pantry();
        party.Inventory.Age(2 * Day - 4 * Hour);
        Assert.Equal(Freshness.Spoiling, stews.Freshness);
        var tossed = party.CampHeal(0.5f, 24_000, new Rng.Lcg());
        Assert.Equal([new Tossed(stews.Item, 3)], tossed);
        Assert.Null(party.Inventory.Find(Stew.Id));
        Assert.Equal("Spoiled and tossed: 3 Meat Stew.", Spoilage.Notice(tossed));
    }

    [Fact]
    public void TheSlipSaysHowLongTheOldestKeeps()
    {
        Assert.Equal("Keeps 7 more days", Spoilage.Describe(7 * Day));
        Assert.Equal("Keeps 2 more days", Spoilage.Describe(Day + 1));
        Assert.Equal("Spoils within a day", Spoilage.Describe(Day));
        Assert.Equal("Spoils within a day", Spoilage.Describe(8 * Hour + 1));
        Assert.Equal("Spoils by the next camp", Spoilage.Describe(8 * Hour));
        Assert.Equal("Spoiled and tossed: 2 Meat Stew, 1 Apple.",
            Spoilage.Notice([new Tossed(Item.From(Stew), 2), new Tossed(Item.From(Apple), 1)]));
    }

    [Fact]
    public void ASaveKeepsEachUnitsAgeAndAnOlderOneLoadsFresh()
    {
        var (party, apples, stews, oats) = Pantry();
        party.Inventory.Age(Day);
        party.Inventory.Add(Apple.Id);
        var saved = CharacterSaves.Save(party);
        Assert.Equal([Day, Day, 0UL], saved.Inventory.Single(s => s.Item == Apple.Id).Ages);
        Assert.Null(saved.Inventory.Single(s => s.Item == Oats.Id).Ages);

        var flags = new FlagNetwork(Enumerable.Range(0, 4).Select(i => new FlagRecord(ItemData.Flag(i))), ItemData.Flag(3));
        var loaded = new CharacterParty(Catalog());
        var problems = new List<string>();
        new CharacterSaves(PluginSet.Load([]), Characters.Rules, loaded.Inventory.Catalog, flags, problems).Load(loaded, saved);
        Assert.Empty(problems);
        Assert.Equal([Day, Day, 0UL], loaded.Inventory.Find(Apple.Id)!.Ages);
        Assert.Equal([Day, Day, Day], loaded.Inventory.Find(Stew.Id)!.Ages);
        Assert.Empty(loaded.Inventory.Find(Oats.Id)!.Ages);

        // Before spoilage, a save had no ages: everything loads fresh, and ages in any order load oldest first.
        var older = saved with { Inventory = [.. saved.Inventory.Select(s => s with { Ages = s.Item == Stew.Id ? [0UL, Day, Hour] : null })] };
        var fresh = new CharacterParty(Catalog());
        new CharacterSaves(PluginSet.Load([]), Characters.Rules, fresh.Inventory.Catalog, flags, problems).Load(fresh, older);
        Assert.Equal([0UL, 0UL, 0UL], fresh.Inventory.Find(Apple.Id)!.Ages);
        Assert.Equal([Day, Hour, 0UL], fresh.Inventory.Find(Stew.Id)!.Ages);
    }
}
