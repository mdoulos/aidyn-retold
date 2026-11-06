using System.Numerics;
using AidynRetold.Core.Combat;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>Guests, an encounter's allies who fight beside the party for that battle (the user's design, Nov 5, 2026).</summary>
public class GuestTests
{
    private static RecordId Entity(int n) => RecordId.Hex(RecordTypes.Entity, n);

    [Fact]
    public void AnEncountersAlliesAndFieldSizeReachTheSetup()
    {
        var party = new MonsterPartyObject(0, Vector3.Zero, 20, PakVariant.Any, null, [new PartyEnemy(Entity(0x300), 2, 4)], Entity(0x300), null, 0, 6, null,
            PartyFlags.Walks, null, null, OnField: 6, Allies: [Entity(0x248), Entity(0x249)]);
        var setup = BattleSetup.WithParty(BattleSource.Wanderer, RecordId.Parse("aidyn:voxel/0x0010/0"), party, null, null, ambush: false);
        Assert.Equal(6, setup.OnField);
        Assert.Equal([Entity(0x248), Entity(0x249)], setup.Guests);

        // An encounter that says nothing leaves both to the rules.
        var plain = party with { OnField = null, Allies = null };
        var quiet = BattleSetup.WithParty(BattleSource.Wanderer, RecordId.Parse("aidyn:voxel/0x0010/0"), plain, null, null, ambush: false);
        Assert.Null(quiet.OnField);
        Assert.Empty(quiet.Guests);
        Assert.Equal([Entity(0x248), Entity(0x249)], setup.Against(RecordId.Parse("aidyn:voxel/0x0010/1"), party).Guests);
    }

    [Fact]
    public void ThePartysSideHasAFrameAMemberAndGuestTheFirstSixLargeTheRestSmallInAColumnBefore()
    {
        var members = Enumerable.Range(0, 5).Select(i => Entity(0x23F + i)).ToList();
        var guests = Enumerable.Range(0, 7).Select(i => Entity(0x300 + i)).ToList();
        var gui = new CombatGui(members, [Entity(0x26E)], guests);

        var bars = gui.Bars.Where(b => b.Party).ToList();
        Assert.Equal(14, bars.Count);
        Assert.Equal(6, bars.Count(b => b.Large));
        Assert.Equal(2, bars.Count(b => b.Blocked));
        Assert.All(bars.Take(6), b => Assert.Equal(CombatGui.PartyColumn, b.FrameX));
        Assert.All(bars.Skip(6), b => Assert.Equal(CombatGui.PartyColumn2, b.FrameX));
        Assert.Equal(Enumerable.Range(0, 8).Select(i => CombatGui.Top + CombatGui.SmallPitch * i), bars.Skip(6).Select(b => b.FrameY));

        // A guest's body made takes its waiting entry and a frame of the party's side, not a placeholder.
        gui.Assign(guests[0], 5, enemy: false);
        var entry = gui.Portraits.Single(p => p.Entity == guests[0]);
        Assert.Equal((PortraitState.Party, 5), (entry.State, entry.Slot));
        Assert.Equal(2, gui.Portraits.Count(p => p.State == PortraitState.Placeholder));
    }

    [Fact]
    public void APartyOfFourWithNoGuestsLooksAsTheOriginalDid()
    {
        var gui = new CombatGui(Enumerable.Range(0, 4).Select(i => Entity(0x23F + i)).ToList(), [Entity(0x26E)]);
        var bars = gui.Bars.Where(b => b.Party).ToList();
        Assert.Equal(6, bars.Count);
        Assert.All(bars, b => Assert.True(b.Large));
        Assert.Equal(Enumerable.Range(0, 6).Select(i => CombatGui.Top + CombatGui.Pitch * i), bars.Select(b => b.FrameY));
    }
}
