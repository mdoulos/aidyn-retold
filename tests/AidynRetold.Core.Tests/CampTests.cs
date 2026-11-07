using AidynRetold.Core.Camp;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using CharacterParty = AidynRetold.Core.Characters.Party;

namespace AidynRetold.Core.Tests;

/// <summary>The remade game's camp: its seed, the party's arrangement at a campsite, the night's talk, the activities and the camp screen (the user's design, Nov 7, 2026).</summary>
public class CampTests
{
    private static readonly RecordId Alaron = GameState.Alaron, Brenna = RecordId.Hex(RecordTypes.Entity, 0x0243),
        Sholeh = RecordId.Hex(RecordTypes.Entity, 0x024D), Abrecan = RecordId.Hex(RecordTypes.Entity, 0x023E), Becan = RecordId.Hex(RecordTypes.Entity, 0x0242);

    private static readonly RecordId Picture = RecordId.Parse("acre:image/campsite_forest");

    private static RecordId Image(string name) => RecordId.Parse($"acre:image/{name.ToLowerInvariant()}");

    /// <summary>A forest with the seven slots the brief asks for, and a layout or two for each party size.</summary>
    private static CampsiteRecord Forest(bool mirror = true) => new(RecordId.Parse("acre:campsite/forest"), "forest", Picture, 1672, 941,
        new CampPoint(0.5f, 0.6f),
        [
            new CampSlot("left_profile", 0.38f, 0.64f, 0.95f, 20, [CampPoses.SitProfile, CampPoses.StandProfile], CampFacing.Right, mirror),
            new CampSlot("right_profile", 0.62f, 0.64f, 0.95f, 20, [CampPoses.SitProfile, CampPoses.StandProfile], CampFacing.Left, mirror),
            new CampSlot("rear_left", 0.44f, 0.55f, 0.85f, 10, [CampPoses.SitRear3Q, CampPoses.StandRear3Q], CampFacing.Away),
            new CampSlot("rear_center", 0.51f, 0.54f, 0.84f, 10, [CampPoses.SitRear, CampPoses.StandRear, CampPoses.StandRear3Q], CampFacing.Away),
            new CampSlot("rear_right", 0.58f, 0.55f, 0.85f, 10, [CampPoses.SitRear3Q, CampPoses.StandRear3Q], CampFacing.Away),
            new CampSlot("near_left", 0.42f, 0.70f, 1.05f, 30, [CampPoses.SitRear, CampPoses.SitRear3Q], CampFacing.Away),
            new CampSlot("tend", 0.47f, 0.66f, 1f, 25, [CampPoses.TendFire], CampFacing.Away, Interaction: new CampPoint(0.5f, 0.58f)),
        ],
        [
            new CampLayout(1, ["left_profile"]),
            new CampLayout(2, ["left_profile", "rear_center"]),
            new CampLayout(2, ["right_profile", "rear_left"]),
            new CampLayout(3, ["left_profile", "rear_center", "right_profile"]),
            new CampLayout(4, ["left_profile", "rear_left", "rear_right", "right_profile"]),
            new CampLayout(5, ["left_profile", "rear_left", "rear_center", "rear_right", "right_profile"]),
        ],
        Pot: new CampPoint(0.5f, 0.58f), Terrains: [0, 3, 4]);

    /// <summary>A figure with every pose, its profiles painted facing right and mirrorable.</summary>
    private static CampFigureRecord Figure(RecordId entity, bool mirror = true, params string[] poses) => new(
        RecordId.Parse($"acre:campfigure/{entity.Key.ToLowerInvariant()}"), entity,
        [.. (poses.Length == 0 ? CampPoses.All : poses).Select(p => new CampPose(p, Image($"{entity.Key}_{p}"), 100, 300,
            CampPoses.FacingsOf(p)[0] == CampFacing.Away ? CampFacing.Away : CampFacing.Right, mirror))]);

    private static Dictionary<RecordId, CampFigureRecord> Figures(params CampFigureRecord[] figures) => figures.ToDictionary(f => f.Entity);

    [Fact]
    public void TheSeedIsTheSameWhateverOrderThePartyIsListedInAndChangesWithTheDay()
    {
        var one = CampSeed.Of(12, "aidyn:mapset/0/12,7", [Alaron, Brenna, Sholeh]);
        Assert.Equal(one, CampSeed.Of(12, "aidyn:mapset/0/12,7", [Sholeh, Alaron, Brenna]));
        Assert.NotEqual(one, CampSeed.Of(13, "aidyn:mapset/0/12,7", [Alaron, Brenna, Sholeh]));
        Assert.NotEqual(one, CampSeed.Of(12, "aidyn:mapset/0/12,8", [Alaron, Brenna, Sholeh]));
        Assert.NotEqual(one, CampSeed.Of(12, "aidyn:mapset/0/12,7", [Alaron, Brenna]));
        // FNV-1a's known value for an empty text, and a known text's.
        Assert.Equal(2166136261u, CampSeed.Fnv1a(""));
        Assert.Equal(0xE40C292Cu, CampSeed.Fnv1a("a"));
    }

    [Fact]
    public void EveryMemberIsPlacedOnceAtASlotOfTheSizesLayoutInAPoseItsFigureHasAndTheSameSeedGivesTheSameArrangement()
    {
        var site = Forest();
        var figures = Figures(Figure(Alaron), Figure(Brenna), Figure(Sholeh), Figure(Abrecan), Figure(Becan));
        RecordId[] party = [Alaron, Brenna, Sholeh, Abrecan, Becan];
        foreach (var size in Enumerable.Range(1, 5))
        {
            var members = party.Take(size).ToList();
            var arrangement = CampArranger.Arrange(site, members, figures.GetValueOrDefault, 1234);
            Assert.False(arrangement.Fallback);
            Assert.Equal(size, arrangement.Layout.Size);
            Assert.Equal(members.Select(m => m.ToString()).Order(), arrangement.Placements.Select(p => p.Member.ToString()).Order());
            Assert.Equal(size, arrangement.Placements.Select(p => p.Slot.Id).Distinct().Count());
            foreach (var placement in arrangement.Placements)
            {
                Assert.Contains(placement.Slot.Id, arrangement.Layout.Slots);
                Assert.Contains(placement.Pose, placement.Slot.Poses);
                Assert.NotNull(placement.Image);
                Assert.Equal(placement.Pose, placement.Image!.Pose);
                Assert.Equal(placement.Slot.Facing, placement.Facing);
            }
            Assert.Empty(arrangement.Diagnostics);

            var again = CampArranger.Arrange(site, members, figures.GetValueOrDefault, 1234);
            Assert.Equal(arrangement.Placements.Select(p => (p.Member, p.Slot.Id, p.Pose, p.Mirrored)), again.Placements.Select(p => (p.Member, p.Slot.Id, p.Pose, p.Mirrored)));
        }
    }

    [Fact]
    public void AProfilePaintedFacingRightSitsAtTheLeftSlotAsPaintedAndAtTheRightSlotMirroredOnlyWhereBothAllowIt()
    {
        var site = Forest();
        var figure = Figure(Alaron, mirror: true, CampPoses.SitProfile);
        Assert.True(CampArranger.Fits(figure, site.Slot("left_profile")!));
        Assert.True(CampArranger.Fits(figure, site.Slot("right_profile")!));
        Assert.False(CampArranger.Fits(figure, site.Slot("rear_center")!));

        var unmirrorable = Figure(Alaron, mirror: false, CampPoses.SitProfile);
        Assert.True(CampArranger.Fits(unmirrorable, site.Slot("left_profile")!));
        Assert.False(CampArranger.Fits(unmirrorable, site.Slot("right_profile")!));
        Assert.False(CampArranger.Fits(figure, Forest(mirror: false).Slot("right_profile")!));

        // Alone, the one layout is the left slot, so the figure sits as painted.
        var alone = CampArranger.Arrange(site, [Alaron], _ => figure, 7);
        Assert.Equal("left_profile", alone.Placements[0].Slot.Id);
        Assert.False(alone.Placements[0].Mirrored);
        // Two with only profiles: neither of the forest's layouts for two has two profile slots, so it's the fallback; with a layout that
        // has both, the right profile's slot takes one of them mirrored.
        Assert.True(CampArranger.Arrange(site, [Alaron, Brenna], _ => figure, 7).Fallback);
        var facing = site with { Layouts = [.. site.Layouts, new CampLayout(2, ["left_profile", "right_profile"])] };
        var pair = CampArranger.Arrange(facing, [Alaron, Brenna], _ => figure, 7);
        Assert.False(pair.Fallback);
        Assert.Equal(["left_profile", "right_profile"], pair.Layout.Slots);
        Assert.Contains(pair.Placements, p => p.Slot.Id == "right_profile" && p.Mirrored);
        Assert.Contains(pair.Placements, p => p.Slot.Id == "left_profile" && !p.Mirrored);
    }

    [Fact]
    public void AMemberWithNoFigureIsPlacedWithNothingToDrawAndADiagnostic()
    {
        var arrangement = CampArranger.Arrange(Forest(), [Alaron, Brenna], id => id == Alaron ? Figure(Alaron) : null, 99);
        Assert.False(arrangement.Fallback);
        var brenna = Assert.Single(arrangement.Placements, p => p.Member == Brenna);
        Assert.Null(brenna.Image);
        Assert.False(brenna.Drawable);
        Assert.Contains(brenna.Pose, brenna.Slot.Poses);
        Assert.Contains(arrangement.Diagnostics, d => d.Contains("No figure", StringComparison.Ordinal) && d.Contains(Brenna.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void WhenNoLayoutTakesThePartyTheFallbackPlacesEveryoneAndSaysSo()
    {
        // Figures with only a tend-fire pose fit no slot of the two-member layouts.
        var tenders = Figure(Alaron, true, CampPoses.TendFire);
        var arrangement = CampArranger.Arrange(Forest(), [Alaron, Brenna], _ => tenders, 5);
        Assert.True(arrangement.Fallback);
        Assert.Equal(2, arrangement.Placements.Count);
        Assert.Equal(2, arrangement.Placements.Select(p => p.Slot.Id).Distinct().Count());
        Assert.All(arrangement.Placements, p => Assert.Null(p.Image));
        Assert.Contains(arrangement.Diagnostics, d => d.Contains("fallback", StringComparison.Ordinal));

        // A size with no layout at all falls back to the slots in order, and says which it lacks.
        var noLayouts = Forest() with { Layouts = [] };
        var three = CampArranger.Arrange(noLayouts, [Alaron, Brenna, Sholeh], _ => Figure(Alaron), 5);
        Assert.True(three.Fallback);
        Assert.Equal(3, three.Placements.Select(p => p.Slot.Id).Distinct().Count());
        Assert.Contains(three.Diagnostics, d => d.Contains("no layout for a party of 3", StringComparison.Ordinal));
    }

    [Fact]
    public void ABadLayoutIsSkippedWithADiagnosticAndSixMembersAreCutToFive()
    {
        var site = Forest() with
        {
            Layouts = [new CampLayout(2, ["left_profile", "left_profile"]), new CampLayout(2, ["left_profile", "nowhere"]), new CampLayout(2, ["left_profile", "rear_center"])],
        };
        var arrangement = CampArranger.Arrange(site, [Alaron, Brenna], _ => Figure(Alaron), 3);
        Assert.False(arrangement.Fallback);
        Assert.Equal(["left_profile", "rear_center"], arrangement.Layout.Slots);
        Assert.Equal(2, arrangement.Diagnostics.Count(d => d.Contains("layout for 2", StringComparison.Ordinal)));

        var six = CampArranger.Arrange(Forest(), [Alaron, Brenna, Sholeh, Abrecan, Becan, RecordId.Hex(RecordTypes.Entity, 0x0240)], _ => Figure(Alaron), 3);
        Assert.Equal(5, six.Placements.Count);
        Assert.Contains(six.Diagnostics, d => d.Contains("at most 5", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => CampArranger.Arrange(Forest(), [], _ => null, 3));
    }

    [Fact]
    public void AnotherLayoutIsPreferredToYesterdaysWhereTheSizeHasOne()
    {
        var site = Forest();
        var figure = Figure(Alaron);
        var yesterday = CampArranger.Arrange(site, [Alaron, Brenna], _ => figure, 11);
        foreach (var seed in Enumerable.Range(0, 20))
        {
            var today = CampArranger.Arrange(site, [Alaron, Brenna], _ => figure, (uint)seed, avoid: yesterday.Layout);
            Assert.False(today.Layout.Slots.SequenceEqual(yesterday.Layout.Slots));
        }
    }

    [Fact]
    public void TheFurthestPlacementIsDrawnFirstAndTheScaleIsTheSlotsTimesTheStature()
    {
        var site = Forest();
        var big = Figure(Abrecan) with { Stature = 1.15f };
        var arrangement = CampArranger.Arrange(site, [Alaron, Brenna, Sholeh, Abrecan], id => id == Abrecan ? big : Figure(id), 2);
        var depths = arrangement.InDrawOrder.Select(p => p.Slot.Depth).ToList();
        Assert.Equal(depths.Order(), depths);
        var abrecan = Assert.Single(arrangement.Placements, p => p.Member == Abrecan);
        Assert.Equal(abrecan.Slot.Scale * 1.15f, abrecan.Scale, 0.0001f);
    }

    private static CampTalkRecord Talk(string name, RecordId[]? members = null, RecordId[]? anyone = null, FlagGate[]? flags = null, int[]? terrains = null,
        RecordId? done = null, int weight = 1) =>
        new(RecordId.Parse($"acre:camptalk/{name}"), RecordId.Parse($"acre:dialogue/{name}"), members, anyone, flags, terrains, done, weight);

    [Fact]
    public void ATalkIsToldOnlyWithItsMembersInThePartyItsFlagsHoldingOnItsTerrainAndNotOnceDone()
    {
        var done = Ids.Flag(5);
        var gate = new FlagGate(Ids.Flag(6));
        CampTalkRecord[] talks =
        [
            Talk("brenna_home", members: [Brenna]),
            Talk("either", anyone: [Sholeh, Abrecan]),
            Talk("after_shamsuk", flags: [gate]),
            Talk("in_the_snow", terrains: [4]),
            Talk("told", done: done),
            Talk("anywhere"),
        ];
        var set = new HashSet<RecordId>();
        IReadOnlyList<CampTalkRecord> Eligible(RecordId[] party, int? terrain) =>
            [.. CampTalks.Eligible(talks, party, g => set.Contains(g.Flag) != g.Invert, set.Contains, terrain)];

        // The one with a done flag is told until the flag is set.
        Assert.Equal(["anywhere", "told"], Eligible([Alaron], 0).Select(t => t.Id.Key));
        set.Add(done);
        Assert.Equal(["anywhere"], Eligible([Alaron], 0).Select(t => t.Id.Key));
        Assert.Equal(["anywhere", "brenna_home"], Eligible([Alaron, Brenna], 0).Select(t => t.Id.Key));
        Assert.Equal(["anywhere", "either"], Eligible([Alaron, Abrecan], 0).Select(t => t.Id.Key));
        Assert.Equal(["anywhere", "in_the_snow"], Eligible([Alaron], 4).Select(t => t.Id.Key));
        Assert.Equal(["anywhere"], Eligible([Alaron], null).Select(t => t.Id.Key));
        set.Add(gate.Flag);
        Assert.Equal(["after_shamsuk", "anywhere"], Eligible([Alaron], 0).Select(t => t.Id.Key));
        set.Remove(gate.Flag);
        Assert.Equal(["anywhere"], Eligible([Alaron], 0).Select(t => t.Id.Key));
    }

    [Fact]
    public void TheTalkIsChosenByWeightOnTheSeedAndTheSameSeedChoosesTheSame()
    {
        CampTalkRecord[] talks = [Talk("common", weight: 9), Talk("rare", weight: 1), Talk("odd", weight: 0)];
        var chosen = Enumerable.Range(0, 200).Select(seed => CampTalks.Choose(talks, [Alaron], _ => true, _ => false, 0, (uint)seed)!.Id.Key).ToList();
        Assert.Equal(chosen, Enumerable.Range(0, 200).Select(seed => CampTalks.Choose(talks, [Alaron], _ => true, _ => false, 0, (uint)seed)!.Id.Key));
        Assert.Contains("common", chosen);
        Assert.Contains("rare", chosen);
        Assert.Contains("odd", chosen);
        Assert.True(chosen.Count(k => k == "common") > chosen.Count(k => k == "rare") * 3);
        Assert.Null(CampTalks.Choose([Talk("brenna", members: [Brenna])], [Alaron], _ => true, _ => false, 0, 1));
    }

    // The activities: brewing from a reagent, which the original's Alchemist does, with the test rules' potion.
    private static readonly ItemRecord Reagent = ItemData.NewItem(0x2011) with { Name = "Herb" };
    private static readonly ItemRecord Potion = ItemData.NewItem(0x2012, ItemKind.Potion) with { Name = "Tonic" };
    private static readonly RecipeRecord Tonic = new(RecordId.Parse("acre:recipe/tonic"), Crafting.Alchemist, Potion.Id, [new Ingredient(Reagent.Id, 1)], 1, 0);
    private static readonly CampActivityRecord Brewing = new(RecordId.Parse("acre:campactivity/brewing"), "Brewing", Crafting.Alchemist, 2, "Potions from reagents.");
    private static readonly CampActivityRecord Smithing = new(RecordId.Parse("acre:campactivity/smithing"), "Making armor", Crafting.Mechanic, 3, Order: -1);
    private static readonly CampActivityRecord Odd = new(RecordId.Parse("acre:campactivity/odd"), "Odd", RecordId.Parse("acre:item/0x0001"), 1);

    private static (CharacterParty Party, Character Alchemist, Character Fighter) Camp(int herbs = 3)
    {
        var party = new CharacterParty(ItemData.Catalog(Reagent, Potion));
        var alchemist = Characters.Create(Characters.Alaron() with { Skills = new SkillLevels(3, 0, 0, 0, 0, 0, 0, 1, 0, 1, 3, 1) });
        var fighter = Characters.Create(Characters.Alaron() with { Id = Abrecan, Name = "Abrecan" });
        party.Add(alchemist);
        party.Add(fighter);
        for (var i = 0; i < herbs; i++)
            party.Inventory.Add(Reagent.Id);
        return (party, alchemist, fighter);
    }

    [Fact]
    public void AnActivityIsOfferedToTheMemberBestAtItsSkillInOrderAndOneNobodyHasOrWithNothingToMakeIsntAvailable()
    {
        var (party, alchemist, _) = Camp();
        var offers = CampActivities.Offered([Brewing, Smithing, Odd], [Tonic], party);
        Assert.Equal(["Making armor", "Brewing"], offers.Select(o => o.Activity.Name));
        var brewing = offers[1];
        Assert.Same(alchemist, brewing.Member);
        Assert.Equal([Tonic], brewing.Recipes);
        Assert.True(brewing.Available);
        var smithing = offers[0];
        Assert.Null(smithing.Member);
        Assert.False(smithing.Available);

        var (hungry, _, _) = Camp(herbs: 0);
        var nothing = CampActivities.Offered([Brewing], [Tonic], hungry)[0];
        Assert.NotNull(nothing.Member);
        // The Alchemist's recipes are offered by level, not by what's in the bag, as the original's popup lists them; making one then says what's missing.
        Assert.Equal([Tonic], nothing.Recipes);
    }

    /// <summary>A screen as it opens, with no campsite: the fire centred and the band away.</summary>
    private static CampMenu Fresh(CharacterParty party, int baseHours = 8, params int[] rolls) =>
        new(party, null, null, CampActivities.Offered([Brewing, Smithing], [Tonic], party), [Tonic], baseHours, Characters.D100(rolls.Length == 0 ? [1, 1, 1, 1, 1, 1] : rolls));

    /// <summary>A screen with its band open, as the session has it once the pan is up and the scroll unrolled.</summary>
    private static CampMenu Screen(CharacterParty party, int baseHours = 8, params int[] rolls)
    {
        var screen = Fresh(party, baseHours, rolls);
        Unroll(screen);
        return screen;
    }

    /// <summary>Ticks the screen until its band is open, as the session does: the pan up, then the scroll unrolled, its actor's clock past any state's end.</summary>
    private static void Unroll(CampMenu screen)
    {
        for (var i = 0; i < 2 * CampMenu.PanTicks && !screen.Open; i++)
            screen.Step(1000, 1);
        Assert.True(screen.Open);
    }

    /// <summary>Ticks the screen until its band has rolled up; how many ticks it took.</summary>
    private static int RollUp(CampMenu screen)
    {
        for (var ticks = 1; ticks <= 100; ticks++)
        {
            if (screen.Step(1000, 1).Closed)
                return ticks;
        }
        Assert.Fail("The band never rolled up.");
        return 0;
    }

    [Fact]
    public void TheBandUnrollsAtTheFootOnceTheTalkIsOverAndRollsUpBeforeRestIsCarriedOut()
    {
        var (party, _, _) = Camp();
        var screen = Fresh(party);
        screen.Talking = true;
        Assert.Equal(0, screen.Pan);
        for (var i = 0; i < 10; i++)
            Assert.Equal(default, screen.Step(1000, 1));
        Assert.Equal(0, screen.Pan);
        Assert.Null(screen.Scroll);
        Assert.False(screen.BandShown);

        // The talk over: the pan takes its second up to the foot, and there the band's scroll starts afresh and unrolls.
        screen.Talking = false;
        CampTick tick = default;
        var ticks = 0;
        while (screen.Scroll is null)
        {
            tick = screen.Step(1000, 1);
            ticks++;
        }
        Assert.Equal(CampMenu.PanTicks, ticks);
        Assert.Equal(1, screen.Pan);
        Assert.True(tick.Restart);
        Assert.NotNull(tick.Cue);
        Assert.True(screen.BandShown);
        Assert.False(screen.Open);
        Assert.False(screen.Press(MenuAction.Right));
        screen.Step(1000, 1);
        Assert.True(screen.Open);

        // Rest: the band rolls up first, nothing taking presses meanwhile, and Rest is carried out as it has; nothing unrolls again.
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampChoice.Rest, screen.Choice);
        Assert.False(screen.Open);
        tick = screen.Step(1000, 1);
        Assert.NotNull(tick.Cue);
        Assert.False(tick.Closed);
        Assert.False(screen.Press(MenuAction.Right));
        Assert.True(RollUp(screen) > 0);
        Assert.Null(screen.Scroll);
        Assert.False(screen.BandShown);
        Assert.Equal(default, screen.Step(1000, 1));
        Assert.Equal(1, screen.Pan);
    }

    [Fact]
    public void TheDebugButtonsTalkRollsTheBandUpFirstAndTheBandComesBackAsTheTalkEnds()
    {
        var (party, _, _) = Camp();
        var screen = Screen(party);
        Assert.True(screen.RequestTalk());
        Assert.True(screen.TalkPending);
        Assert.False(screen.Open);
        Assert.False(screen.RequestTalk());
        RollUp(screen);
        Assert.Equal(1, screen.Pan);

        // The session starts the talk as the band has rolled up: the picture pans back down to the fire under the box.
        screen.TalkPending = false;
        screen.Talking = true;
        for (var i = 0; i < CampMenu.PanTicks; i++)
            Assert.Equal(default, screen.Step(1000, 1));
        Assert.Equal(0, screen.Pan);
        Assert.Null(screen.Scroll);

        // And as it ends, the pan goes up again and the band unrolls afresh.
        screen.Talking = false;
        Unroll(screen);
        Assert.Equal(1, screen.Pan);
        Assert.Equal(CampTab.Camp, screen.Tab);
    }

    [Fact]
    public void TheScreenOpensOnTheCampTabWhereAIsRestAndTheTriggersWalkTheFiveTabsRound()
    {
        var (party, _, _) = Camp();
        var screen = Screen(party);
        Assert.Equal(CampTab.Camp, screen.Tab);
        Assert.Equal("Rest (8 hours)", screen.RestLabel);
        Assert.Equal(2, screen.Members.Count);
        Assert.Null(screen.Empty);

        Assert.True(screen.Press(MenuAction.TabNext));
        Assert.Equal(CampTab.Food, screen.Tab);
        Assert.Null(screen.Current);
        Assert.Equal(CampMenu.NobodyCan, screen.Empty);
        Assert.True(screen.Press(MenuAction.Right));
        Assert.Equal(CampTab.Potions, screen.Tab);
        Assert.True(screen.Press(MenuAction.Right));
        Assert.Equal(CampTab.Weapons, screen.Tab);
        Assert.Equal("", screen.Empty);
        Assert.True(screen.Press(MenuAction.Right));
        Assert.Equal(CampTab.Armor, screen.Tab);
        Assert.Equal(CampMenu.NobodyCan, screen.Empty);
        Assert.True(screen.Press(MenuAction.Right));
        Assert.Equal(CampTab.Camp, screen.Tab);
        Assert.True(screen.Press(MenuAction.TabPrevious));
        Assert.Equal(CampTab.Armor, screen.Tab);

        screen.Show(CampTab.Camp);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampChoice.Rest, screen.Choice);
        Assert.False(screen.Press(MenuAction.Up));
    }

    [Fact]
    public void ThePotionsTabOffersTheAlchemistsRecipesAndEachThingMadeAddsItsActivitysHours()
    {
        var (party, alchemist, _) = Camp();
        var screen = Screen(party);
        screen.Show(CampTab.Potions);
        Assert.Same(alchemist, screen.Member);
        Assert.Equal([Tonic], screen.Recipes);
        Assert.Null(screen.Empty);

        // Two tonics: two herbs used, two hours each.
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal("Tonic is ready.", screen.Message);
        Assert.Equal(10, screen.Hours);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(12, screen.Hours);
        Assert.Equal(2, party.Inventory.Count(Potion.Id));
        Assert.Equal(1, party.Inventory.Count(Reagent.Id));
        Assert.Equal(2, screen.Made["Tonic"]);
        Assert.Equal(2, screen.Log.Count(l => l == "Tonic is ready."));
        Assert.Equal("Rest (12 hours)", screen.RestLabel);

        // A on Armor, which nobody can do, only says so.
        screen.Show(CampTab.Armor);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampMenu.NobodyCan, screen.Message);
        Assert.Equal(12, screen.Hours);
    }

    [Fact]
    public void AFailedBrewAddsNoHoursAndMissingIngredientsAreSaid()
    {
        var (party, _, _) = Camp(herbs: 1);
        // A roll of 100 fails against Intelligence × 3 + Alchemist × 6, 72; the herb is used up all the same, as the original's brewing uses it.
        var screen = Screen(party, 8, 100, 1);
        screen.Show(CampTab.Potions);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal("Alaron fails.", screen.Message);
        Assert.Equal(8, screen.Hours);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampMenu.MissingIngredients, screen.Message);
        Assert.Equal(8, screen.Hours);
        Assert.Empty(screen.Made);
    }

    [Fact]
    public void BBreaksCampAndTheTalkTakesThePressesWhileItShows()
    {
        var (party, _, _) = Camp();
        var screen = Screen(party);
        screen.Talking = true;
        Assert.False(screen.Press(MenuAction.Back));
        screen.Talking = false;
        Assert.True(screen.Press(MenuAction.Back));
        Assert.Equal(CampChoice.Leave, screen.Choice);
        Assert.False(screen.Press(MenuAction.Accept));
    }

    [Fact]
    public void CLeftAndCRightChangeWhoBrewsAmongThoseWithTheSkillAndTheMouseCanPointAtRecipes()
    {
        var (party, alchemist, fighter) = Camp();
        // A second alchemist at 1.
        var second = Characters.Create(Characters.Alaron() with { Id = Becan, Name = "Becan", Skills = new SkillLevels(1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 3, 1) });
        party.Add(second);
        var screen = Screen(party);
        screen.Show(CampTab.Potions);
        Assert.Same(alchemist, screen.Member);
        Assert.True(screen.Press(MenuAction.MemberRight));
        Assert.Same(second, screen.Member);
        Assert.True(screen.Press(MenuAction.MemberRight));
        Assert.Same(alchemist, screen.Member);
        Assert.True(screen.Press(MenuAction.MemberLeft));
        Assert.Same(second, screen.Member);
        Assert.NotSame(fighter, screen.Member);
        Assert.True(screen.PointRecipe(0));
        Assert.False(screen.PointRecipe(1));
    }
}
