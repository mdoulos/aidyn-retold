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
            new CampSlot("rear_left", 0.44f, 0.55f, 0.85f, 10, [CampPoses.SitRear3Q, CampPoses.StandRear3Q], CampFacing.Right, mirror),
            new CampSlot("rear_center", 0.51f, 0.54f, 0.84f, 10, [CampPoses.SitRear, CampPoses.StandRear, CampPoses.StandRear3Q], CampFacing.Away),
            new CampSlot("rear_right", 0.58f, 0.55f, 0.85f, 10, [CampPoses.SitRear3Q, CampPoses.StandRear3Q], CampFacing.Left, mirror),
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

    /// <summary>A figure with every pose, its profiles and three-quarter rears painted facing right and mirrorable.</summary>
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
        // Images painted at the picture's scale are drawn at it; with the campsite's canonical height and the figure's own in its images,
        // scaled from one to the other (the user's Alaron, 718 tall in his, to the forest's 230).
        Assert.Equal(abrecan.Scale, abrecan.PictureScale(site), 0.0001f);
        var scaled = abrecan with { Figure = big with { Height = 718 } };
        Assert.Equal(abrecan.Scale, scaled.PictureScale(site), 0.0001f);
        Assert.Equal(abrecan.Scale * 230f / 718, scaled.PictureScale(site with { FigureHeight = 230 }), 0.0001f);
    }

    /// <summary>The user's Alaron (Nov 7, 2026): each pose painted facing both ways, never mirrored, as his one pauldron can't be.</summary>
    private static CampFigureRecord BothWays(RecordId entity) => new(
        RecordId.Parse($"acre:campfigure/{entity.Key.ToLowerInvariant()}"), entity,
        [.. new[] { CampPoses.StandProfile, CampPoses.SitProfile, CampPoses.StandRear3Q, CampPoses.SitRear3Q }
            .SelectMany(p => new[] { CampFacing.Right, CampFacing.Left }.Select(f => new CampPose(p, Image($"{entity.Key}_{p}_{f}"), 100, 300, f)))],
        Height: 718);

    [Fact]
    public void AFigurePaintedBothWaysTakesEachSideAsPaintedAndThreeQuarterRearsFaceTheFire()
    {
        Assert.Equal([CampFacing.Left, CampFacing.Right], CampPoses.FacingsOf(CampPoses.SitRear3Q));
        Assert.Equal([CampFacing.Away], CampPoses.FacingsOf(CampPoses.SitRear));
        // The front three-quarters are like the rear ones: either way, and one of them seated.
        Assert.Equal([CampFacing.Left, CampFacing.Right], CampPoses.FacingsOf(CampPoses.StandFront3Q));
        Assert.Equal([CampFacing.Left, CampFacing.Right], CampPoses.FacingsOf(CampPoses.SitFront3Q));
        Assert.True(CampPoses.IsSeated(CampPoses.SitFront3Q));
        Assert.False(CampPoses.IsSeated(CampPoses.StandFront3Q));
        Assert.True(CampPoses.IsKnown(CampPoses.StandFront3Q));
        var figure = BothWays(Alaron);
        Assert.Equal(Image($"{Alaron.Key}_{CampPoses.SitProfile}_{CampFacing.Left}"), figure.PoseOf(CampPoses.SitProfile, CampFacing.Left)!.Image);
        Assert.Null(figure.PoseOf(CampPoses.SitRear, CampFacing.Away));
        // Where the slots don't allow mirroring, each side still takes the image painted facing its way.
        var site = Forest(mirror: false);
        foreach (var id in new[] { "left_profile", "right_profile", "rear_left", "rear_right" })
            Assert.True(CampArranger.Fits(figure, site.Slot(id)!), id);
        Assert.False(CampArranger.Fits(figure, site.Slot("rear_center")!));
        var four = CampArranger.Arrange(site, [Alaron, Brenna, Sholeh, Abrecan], _ => figure, 21);
        Assert.False(four.Fallback);
        Assert.All(four.Placements, p =>
        {
            Assert.False(p.Mirrored);
            Assert.Equal(p.Slot.Facing, p.Image!.Facing);
        });
    }

    [Fact]
    public void AFigureStandsAsTallAsItsEntityAndItsBodySizeSetsHowWideItsShadowsAre()
    {
        var figure = BothWays(Becan);
        var becan = Characters.Alaron() with { Body = new EntityBody(Image("actor"), 0.7f, 0.907f, 2.04f), BodySize = BodySize.LargeMan };
        var sized = figure.Sized(becan);
        Assert.Equal(2.04f * 0.907f / 1.82f, sized.Stature!.Value, 0.0001f);
        Assert.Equal(1.25f, sized.ShadowWidth);
        var placement = CampArranger.Preview(Forest(), sized, 1).Placements[0];
        Assert.Equal(placement.Slot.Scale * sized.Stature.Value, placement.Scale, 0.0001f);
        // The canonical height is a stature of 1, and no size given is a normal body's.
        var alaron = figure.Sized(Characters.Alaron() with { Body = new EntityBody(Image("actor"), 0.7f, 1, CampFigureRecord.CanonicalHeight) });
        Assert.Equal(1, alaron.Stature!.Value, 0.0001f);
        Assert.Equal(1, alaron.ShadowWidth);
        Assert.Equal(1, BodySizes.ShadowWidth(BodySize.NormalMan));
        Assert.Equal(1, BodySizes.ShadowWidth(BodySize.NormalWoman));
        Assert.Equal(1.5f, BodySizes.ShadowWidth(BodySize.GiantMan));
        // A stature of the figure's own stands, and with no entity it stands at 1.
        Assert.Equal(1.15f, (figure with { Stature = 1.15f }).Sized(becan).Stature!.Value, 0.0001f);
        Assert.Equal(1, figure.Sized(null).Stature!.Value, 0.0001f);
    }

    [Fact]
    public void ThePreviewOfSeveralFiguresFillsEverySlotOneOfThemFitsAndSharesOutThoseMoreThanOneDoes()
    {
        var site = Forest(mirror: false);
        var alaron = BothWays(Alaron);
        // Painted facing right alone, as Becan's first sheet was (Nov 8, 2026).
        var becan = BothWays(Becan) is var both ? both with { Poses = [.. both.Poses.Where(p => p.Facing == CampFacing.Right)] } : null!;
        var preview = CampArranger.Preview(site, [becan, alaron], 1234);
        Assert.Equal(["left_profile", "right_profile", "rear_left", "rear_right"], preview.Placements.Select(p => p.Slot.Id));
        Assert.Equal([Becan, Alaron, Becan, Alaron], preview.Placements.Select(p => p.Member));
        Assert.All(preview.Placements, p => Assert.Equal(p.Slot.Facing, p.Image!.Facing));
    }

    [Fact]
    public void ThePreviewShowsAFigureAtEverySlotItFitsAndTheSeedSitsOrStandsEachOneOnItsOwn()
    {
        var site = Forest();
        var figure = BothWays(Alaron);
        var preview = CampArranger.Preview(site, figure, 1234);
        Assert.Equal(["left_profile", "right_profile", "rear_left", "rear_right"], preview.Placements.Select(p => p.Slot.Id));
        Assert.Equal(preview.Placements.Select(p => p.Slot.Id), preview.Layout.Slots);
        Assert.Empty(preview.Diagnostics);
        Assert.All(preview.Placements, p =>
        {
            Assert.Equal(Alaron, p.Member);
            Assert.Contains(p.Pose, p.Slot.Poses);
            Assert.Equal(p.Slot.Facing, p.Image!.Facing);
        });
        // The seed chooses each slot's pose by its high bits, so the four aren't tied to each other: over a run of seeds, most of the
        // sixteen ways the four can sit or stand turn up, where the generator's low bit alone would alternate them.
        var ways = Enumerable.Range(0, 64).Select(seed => string.Join(",", CampArranger.Preview(site, figure, (uint)(seed * 7919 + 1)).Placements.Select(p => p.Pose))).Distinct().Count();
        Assert.True(ways >= 12, $"{ways} ways");
        // A figure that fits nowhere is shown nowhere, and that's said.
        var tender = Figure(Alaron, true, CampPoses.TendFire) with { Poses = [] };
        Assert.Contains(CampArranger.Preview(site, tender, 1).Diagnostics, d => d.Contains("fits none", StringComparison.Ordinal));
    }

    [Fact]
    public void ReposingSitsOrStandsTheFiguresAfreshWhereTheyAreAndChangesAPoseEachTime()
    {
        var site = Forest();
        var figure = BothWays(Alaron);
        var arrangement = CampArranger.Arrange(site, [Alaron, Brenna, Sholeh, Abrecan], _ => figure, 8);
        var random = new Rng.Lcg(99);
        for (var press = 0; press < 20; press++)
        {
            var reposed = CampArranger.Repose(arrangement, random);
            Assert.Equal(arrangement.Layout, reposed.Layout);
            Assert.Equal(arrangement.Placements.Select(p => (p.Member, p.Slot.Id)), reposed.Placements.Select(p => (p.Member, p.Slot.Id)));
            Assert.Contains(reposed.Placements.Zip(arrangement.Placements), pair => pair.First.Pose != pair.Second.Pose);
            Assert.All(reposed.Placements, p => Assert.Equal(p.Pose, p.Image!.Pose));
            arrangement = reposed;
        }
        // One with nothing to change keeps it, a member with no figure kept as they were.
        var lone = CampArranger.Arrange(site, [Alaron, Brenna], id => id == Alaron ? Figure(Alaron, true, CampPoses.SitProfile, CampPoses.SitRear) : null, 4);
        Assert.Equal(lone.Placements, CampArranger.Repose(lone, random).Placements);
    }

    [Fact]
    public void TheDebugChoiceForASlotPutsAFigureThereStandingOrSittingOrEmptiesIt()
    {
        var site = Forest();
        var figure = BothWays(Alaron);
        Assert.Equal(CampPoses.StandProfile, CampArranger.Pick(figure, site.Slot("left_profile")!, seated: false)!.Value.Pose);
        Assert.Equal(CampPoses.SitRear3Q, CampArranger.Pick(figure, site.Slot("rear_right")!, seated: true)!.Value.Pose);
        Assert.Null(CampArranger.Pick(figure, site.Slot("rear_center")!, seated: true));

        var preview = CampArranger.Preview(site, figure, 5);
        // Sitting at the left profile, whatever it was; then standing; the layout the slots taken, in the campsite's order.
        var sitting = CampArranger.Place(preview, "left_profile", figure, seated: true);
        Assert.Equal(CampPoses.SitProfile, Assert.Single(sitting.Placements, p => p.Slot.Id == "left_profile").Pose);
        var standing = CampArranger.Place(sitting, "left_profile", figure, seated: false);
        Assert.Equal(CampPoses.StandProfile, Assert.Single(standing.Placements, p => p.Slot.Id == "left_profile").Pose);
        Assert.Equal(preview.Layout.Slots, standing.Layout.Slots);
        // Emptied, and filled again at the end of the campsite's order rather than the list's.
        var emptied = CampArranger.Place(standing, "left_profile", null, false);
        Assert.Equal(["right_profile", "rear_left", "rear_right"], emptied.Layout.Slots);
        Assert.Equal(3, emptied.Layout.Size);
        Assert.Equal(["left_profile", "right_profile", "rear_left", "rear_right"], CampArranger.Place(emptied, "left_profile", figure, true).Layout.Slots);
        // A pose the figure hasn't there changes nothing; a slot the campsite hasn't is refused.
        Assert.Same(emptied, CampArranger.Place(emptied, "rear_center", figure, seated: true));
        Assert.Throws<ArgumentException>(() => CampArranger.Place(emptied, "nowhere", figure, true));
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
    private static readonly CampActivityRecord Brewing = new(RecordId.Parse("acre:campactivity/brewing"), "Brewing", Crafting.Alchemist, "Potions from reagents.");
    private static readonly CampActivityRecord Smithing = new(RecordId.Parse("acre:campactivity/smithing"), "Making armor", Crafting.Mechanic, Order: -1);
    private static readonly CampActivityRecord Odd = new(RecordId.Parse("acre:campactivity/odd"), "Odd", RecordId.Parse("acre:item/0x0001"));

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
    private static CampMenu Fresh(CharacterParty party, params int[] rolls) =>
        new(party, null, null, CampActivities.Offered([Brewing, Smithing], [Tonic], party), [Tonic], Characters.D100(rolls.Length == 0 ? [1, 1, 1, 1, 1, 1] : rolls));

    /// <summary>A screen with its band open, as the session has it once the pan is up and the scroll unrolled.</summary>
    private static CampMenu Screen(CharacterParty party, params int[] rolls)
    {
        var screen = Fresh(party, rolls);
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
        Assert.True(screen.Press(MenuAction.Right));
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.True(screen.RestListOpen);
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
        Assert.Equal("Long Rest", screen.RestLabel);
        Assert.Equal(2, screen.Members.Count);
        Assert.Null(screen.Empty);

        // On the tabs: down and up walk the list without wrapping, the triggers wrap, and right enters the content.
        Assert.Equal(CampFocus.Tabs, screen.Focus);
        Assert.True(screen.Press(MenuAction.TabNext));
        Assert.Equal(CampTab.Food, screen.Tab);
        Assert.Null(screen.Current);
        Assert.Equal(CampMenu.NobodyCan, screen.Empty);
        Assert.True(screen.Press(MenuAction.Down));
        Assert.Equal(CampTab.Potions, screen.Tab);
        Assert.True(screen.Press(MenuAction.Down));
        Assert.Equal(CampTab.Weapons, screen.Tab);
        Assert.Equal("", screen.Empty);
        Assert.True(screen.Press(MenuAction.Down));
        Assert.Equal(CampTab.Armor, screen.Tab);
        Assert.Equal(CampMenu.NobodyCan, screen.Empty);
        Assert.True(screen.Press(MenuAction.Down));
        Assert.Equal(CampTab.Armor, screen.Tab);
        Assert.True(screen.Press(MenuAction.TabNext));
        Assert.Equal(CampTab.Camp, screen.Tab);
        Assert.True(screen.Press(MenuAction.TabPrevious));
        Assert.Equal(CampTab.Armor, screen.Tab);
        Assert.True(screen.Press(MenuAction.Up));
        Assert.Equal(CampTab.Weapons, screen.Tab);
        Assert.Equal(CampFocus.Tabs, screen.Focus);
        Assert.True(screen.Press(MenuAction.Right));
        Assert.Equal(CampFocus.Content, screen.Focus);
        Assert.True(screen.Press(MenuAction.Down));
        Assert.Equal(CampTab.Weapons, screen.Tab);
        // B in the content goes back to the tabs; left does too; A on the tabs enters the content.
        Assert.True(screen.Press(MenuAction.Back));
        Assert.Equal(CampFocus.Tabs, screen.Focus);
        Assert.Equal(CampChoice.None, screen.Choice);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampFocus.Content, screen.Focus);
        Assert.True(screen.Press(MenuAction.Left));
        Assert.Equal(CampFocus.Tabs, screen.Focus);

        // On Camp, A enters the content, A again opens the ways to rest, and A rests the first, until morning.
        screen.Show(CampTab.Camp);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampChoice.None, screen.Choice);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.True(screen.RestListOpen);
        Assert.Equal(CampChoice.None, screen.Choice);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampChoice.Rest, screen.Choice);
        Assert.Equal(screen.RestOptions[0].Hours, screen.Hours);
        Assert.False(screen.Press(MenuAction.Up));
    }

    [Fact]
    public void ThePotionsTabOffersTheAlchemistsRecipesAndWhatsMadeDoesntChangeTheRest()
    {
        var (party, alchemist, _) = Camp();
        var screen = Screen(party);
        screen.Show(CampTab.Potions);
        screen.Press(MenuAction.Right);
        Assert.Same(alchemist, screen.Member);
        Assert.Equal([Tonic], screen.Recipes);
        Assert.Null(screen.Empty);

        // Two tonics: two herbs used, and the rest no longer for it.
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal("Tonic is ready.", screen.Message);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(0, screen.Hours);
        Assert.Equal(2, party.Inventory.Count(Potion.Id));
        Assert.Equal(1, party.Inventory.Count(Reagent.Id));
        Assert.Equal(2, screen.Made["Tonic"]);
        Assert.Equal(2, screen.Log.Count(l => l == "Tonic is ready."));
        Assert.Equal("Long Rest", screen.RestLabel);
        Assert.Equal(screen.RestOptions.Select(o => o.Hours), Screen(party).RestOptions.Select(o => o.Hours));

        // A on Armor, which nobody can do, only says so.
        screen.Show(CampTab.Armor);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampMenu.NobodyCan, screen.Message);
    }

    [Fact]
    public void LongRestOpensItsWaysWhichWakeAtMorningNoonEveningOrMidnightAndQuickRestIsAnHour()
    {
        Assert.Equal(8.5, CampMenu.HoursUntil(21.5, CampMenu.MorningHour));
        Assert.Equal(14.5, CampMenu.HoursUntil(21.5, CampMenu.NoonHour));
        Assert.Equal(20.5, CampMenu.HoursUntil(21.5, CampMenu.EveningHour));
        // Morning begun, the next one; at its very hour, a whole day.
        Assert.Equal(22, CampMenu.HoursUntil(8, CampMenu.MorningHour));
        Assert.Equal(24, CampMenu.HoursUntil(6, CampMenu.MorningHour));
        Assert.Equal(2.5, CampMenu.HoursUntil(21.5, CampMenu.MidnightHour));

        var (party, _, _) = Camp();
        var screen = new CampMenu(party, null, null, [], [Tonic], Characters.D100([1])) { HourOfDay = 10 };
        Unroll(screen);
        Assert.Equal(["Rest Until Morning", "Rest Until Noon", "Rest Until Evening", "Rest Until Midnight"], screen.RestOptions.Select(o => o.Label));
        Assert.Equal([20.0, 2, 8, 14], screen.RestOptions.Select(o => o.Hours));
        Assert.Equal("Current Time: 10:00", screen.CurrentTimeLine);
        Assert.Equal("Current Time: 21:37", new CampMenu(party, null, null, [], [Tonic], Characters.D100([1])) { HourOfDay = 21 + 37 / 60.0 + 1e-9 }.CurrentTimeLine);

        // Into the content on Long Rest; the list takes the presses while it's open: B shuts it, the tabs stay put, and down to the last
        // rests until midnight.
        screen.Press(MenuAction.Right);
        Assert.Equal(CampRestButton.Long, screen.RestButton);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.True(screen.RestListOpen);
        Assert.True(screen.Press(MenuAction.TabNext));
        Assert.Equal(CampTab.Camp, screen.Tab);
        Assert.True(screen.Press(MenuAction.Back));
        Assert.False(screen.RestListOpen);
        Assert.Equal(CampChoice.None, screen.Choice);
        Assert.Equal(CampFocus.Content, screen.Focus);
        screen.Press(MenuAction.Accept);
        for (var i = 0; i < 5; i++)
            screen.Press(MenuAction.Down);
        Assert.Equal(3, screen.RestCursor);
        Assert.True(screen.PointRestOption(1));
        Assert.False(screen.PointRestOption(9));
        screen.Press(MenuAction.Down);
        screen.Press(MenuAction.Down);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal((CampChoice.Rest, 14.0), (screen.Choice, screen.Hours));

        // Quick Rest, left of Long Rest: an hour at once; left again is the tabs.
        var quick = new CampMenu(party, null, null, [], [Tonic], Characters.D100([1])) { HourOfDay = 10 };
        Unroll(quick);
        quick.Press(MenuAction.Right);
        quick.Press(MenuAction.Left);
        Assert.Equal((CampFocus.Content, CampRestButton.Quick), (quick.Focus, quick.RestButton));
        quick.Press(MenuAction.Left);
        Assert.Equal(CampFocus.Tabs, quick.Focus);
        quick.Press(MenuAction.Right);
        Assert.Equal(CampRestButton.Long, quick.RestButton);
        Assert.False(quick.PointRestButton(CampRestButton.Long));
        Assert.True(quick.PointRestButton(CampRestButton.Quick));
        Assert.True(quick.Press(MenuAction.Accept));
        Assert.False(quick.RestListOpen);
        Assert.Equal((CampChoice.Rest, 1.0), (quick.Choice, quick.Hours));
    }

    [Fact]
    public void AFailedBrewIsSaidAndSoAreMissingIngredients()
    {
        var (party, _, _) = Camp(herbs: 1);
        // A roll of 100 fails against Intelligence × 3 + Alchemist × 6, 72; the herb is used up all the same, as the original's brewing uses it.
        var screen = Screen(party, 100, 1);
        screen.Show(CampTab.Potions);
        screen.Press(MenuAction.Right);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal("Alaron fails.", screen.Message);
        Assert.True(screen.Press(MenuAction.Accept));
        Assert.Equal(CampMenu.MissingIngredients, screen.Message);
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

    [Fact]
    public void TheShadowsComeFromTheLightsStrongEnoughToCastThemAsDarkAsTheirShareOfTheLight()
    {
        var sun = new CampSun(0.55f, 0.5f, -0.67f, new Rgba(255, 214, 150, 255), 0.85f);
        // The forest's day: the sun gives about half the light and casts a shadow about half dark; a day's fire, a touch, casts next to none.
        var ambient = CampLighting.Luminance(new Rgba(160, 158, 142, 255));
        var day = CampLighting.Casters(ambient, CampLighting.Sunlight(sun), 0.19f);
        Assert.Equal(CampLightKind.Sun, day[0].Light);
        Assert.InRange(day[0].Darkness, 0.45f, 0.5f);
        Assert.True(day.Count == 1 || day[1].Darkness < 0.08f);
        // A night: no sun, a dim ambient, the fire at full; the fire casts, dark.
        var night = Assert.Single(CampLighting.Casters(0.25f, CampLighting.Sunlight(null), 0.55f));
        Assert.Equal(CampLightKind.Fire, night.Light);
        Assert.InRange(night.Darkness, 0.65f, 0.72f);
        // A dusk: both cast, the stronger first; a sun under the horizon gives nothing; nothing at all casts nothing.
        var dusk = CampLighting.Casters(0.3f, 0.4f, 0.35f);
        Assert.Equal([CampLightKind.Sun, CampLightKind.Fire], dusk.Select(c => c.Light));
        Assert.Equal(0, CampLighting.Sunlight(sun with { Y = -0.2f }));
        Assert.Empty(CampLighting.Casters(0, 0, 0));
        // A light under the faintest share casts nothing; one over the noticed casts its whole share.
        Assert.Empty(CampLighting.Casters(1, 0.05f, 0));
        Assert.Equal(0.5f, Assert.Single(CampLighting.Casters(1, 1, 0)).Darkness, 0.0001f);
    }

    [Fact]
    public void TheCampsitesLightIsTheOneBegunByTheHourWrappingRoundMidnightOrItsOwnWithoutAny()
    {
        var sun = new CampSun(0.5f, 0.5f, -0.7f, new Rgba(255, 220, 160, 255));
        var plain = Forest() with { Ambient = new Rgba(200, 200, 190, 255), Sun = sun };
        Assert.Equal(new CampLight(0, new Rgba(200, 200, 190, 255), sun), plain.LightAt(21));
        Assert.Equal(new Rgba(255, 255, 255, 255), Forest().LightAt(12).Ambient);
        var night = new CampLight(19, new Rgba(60, 62, 80, 255), Fire: 1);
        var day = new CampLight(6, new Rgba(160, 158, 142, 255), sun, 0.35f);
        var lit = Forest() with { Lights = [night, day] };
        Assert.Same(day, lit.LightAt(6));
        Assert.Same(day, lit.LightAt(18.9));
        Assert.Same(night, lit.LightAt(19));
        Assert.Same(night, lit.LightAt(2));
        Assert.Same(night, lit.LightAt(-1));
        Assert.Same(day, lit.LightAt(30));
    }
}
