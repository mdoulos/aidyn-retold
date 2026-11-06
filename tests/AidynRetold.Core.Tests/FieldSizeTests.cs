using AidynRetold.Core.Combat;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;

namespace AidynRetold.Core.Tests;

/// <summary>How many enemies stand at once, the remade game's data (the user's design, Nov 5, 2026), and the room a battle has for them.</summary>
public class FieldSizeTests
{
    [Fact]
    public void TheEncounterSaysElseTheRulesElseTheOriginalsFourNeverMoreThanTheList()
    {
        Assert.Equal(4, Battle.StartingOnField(10, null, null));
        Assert.Equal(Battle.OriginalOnField, Battle.StartingOnField(12, null, null));
        Assert.Equal(6, Battle.StartingOnField(10, null, 6));
        Assert.Equal(10, Battle.StartingOnField(10, 12, 6));
        Assert.Equal(12, Battle.StartingOnField(12, 12, null));
        Assert.Equal(2, Battle.StartingOnField(2, 12, 6));
        // Out of range numbers are brought back in: at least one, at most the list's twelve.
        Assert.Equal(1, Battle.StartingOnField(5, 0, null));
        Assert.Equal(1, Battle.StartingOnField(5, -3, null));
        Assert.Equal(12, Battle.StartingOnField(12, 40, null));
    }

    [Fact]
    public void ABattleHoldsTwelveASideAndThePoolsFollow()
    {
        Assert.Equal(24, Battle.SlotCount);
        Assert.Equal(14, Battle.OriginalSlotCount);
        Assert.Equal(12, Battle.MaxEnemies);
        Assert.Equal(Battle.SlotCount, SpellFx.MaxBodies);
        Assert.Equal(Battle.SlotCount, CombatNumbers.Pool);
    }

    [Fact]
    public void TwelveEnemiesOnTheFieldHaveFourLargeFramesAndEightSmallInTwoColumns()
    {
        var enemies = Enumerable.Range(0, 12).Select(i => RecordId.Hex(RecordTypes.Entity, 0x300 + i)).ToList();
        var gui = new CombatGui([RecordId.Hex(RecordTypes.Entity, 0x23F)], enemies);
        var bars = gui.Bars.Where(b => !b.Party).ToList();
        Assert.Equal(12, bars.Count);
        Assert.Equal(4, bars.Count(b => b.Large));
        // The small ones in two columns of four, 12 apart from y 130.
        var small = bars.Where(b => !b.Large).ToList();
        Assert.Equal(8, small.Count);
        Assert.Equal(2, small.Select(b => b.X).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 4).Select(i => small[0].Y + CombatGui.SmallPitch * i), small.Take(4).Select(b => b.Y));
        Assert.Equal(small.Take(4).Select(b => b.Y), small.Skip(4).Select(b => b.Y));
    }
}
