using AidynRetold.Plugins.Records;

namespace AidynRetold.Plugins.Tests;

/// <summary>The remade game's camp records: a campsite, a figure, an activity and a talk, as the ACRE plugin writes them (the user's design, Nov 7, 2026).</summary>
public sealed class CampRecordsTests
{
    private const string Campsite = """
        [{
          "id": "acre:campsite/forest", "name": "forest", "image": "acre:image/campsite_forest", "width": 1672, "height": 941,
          "fire": { "x": 0.5, "y": 0.6 }, "pot": { "x": 0.5, "y": 0.58 }, "safeUi": { "x": 0.02, "y": 0.72, "width": 0.96, "height": 0.26 },
          "terrains": [0, 3, 4], "figureHeight": 230, "horizon": 0.3055,
          "sun": { "x": 0.55, "y": 0.5, "z": -0.67, "color": [255, 214, 150, 255], "strength": 0.85 },
          "lights": [ { "from": 6, "ambient": [160, 158, 142, 255], "sun": { "x": 0.55, "y": 0.5, "z": -0.67, "color": [255, 214, 150, 255] }, "fire": 0.35 },
                      { "from": 19, "ambient": [60, 62, 80, 255] } ],
          "campfire": { "image": "acre:image/camp_campfire", "pot": "acre:image/camp_pot", "anchor": { "x": 0.505, "y": 0.75 }, "width": 184,
            "rim": { "x": 0.4975, "y": 0.475 }, "front": "acre:image/camp_rock_front" },
          "slots": [
            { "id": "left_profile", "x": 0.38, "y": 0.64, "scale": 0.95, "depth": 20, "poses": ["sit_profile", "stand_profile"], "facing": "Right", "mirror": true },
            { "id": "rear_center", "x": 0.51, "y": 0.54, "scale": 0.84, "depth": 10, "poses": ["sit_rear", "stand_rear"], "facing": "Away" }
          ],
          "layouts": [ { "size": 1, "slots": ["left_profile"] }, { "size": 2, "slots": ["left_profile", "rear_center"] } ]
        },
        { "id": "acre:campsite/snow", "name": "snow", "image": "acre:image/campsite_snow", "width": 1672, "height": 941,
          "fire": { "x": 0.5, "y": 0.6 }, "slots": [], "layouts": [], "terrains": [8], "ambient": [200, 210, 255, 255], "calibrated": true }]
        """;

    private const string Figure = """
        [{ "id": "acre:campfigure/alaron", "entity": "aidyn:entity/0x023F", "stature": 1.0, "height": 718, "poses": [
          { "pose": "sit_profile", "image": "acre:image/camp_alaron_sit_profile_right", "anchorX": 150, "anchorY": 440, "facing": "Right", "mirror": false,
            "normal": "acre:image/camp_alaron_sit_profile_right_normal" },
          { "pose": "sit_profile", "image": "acre:image/camp_alaron_sit_profile_left", "anchorX": 340, "anchorY": 438, "facing": "Left",
            "feet": [{ "x": 72, "y": 444, "width": 140 }, { "x": 94, "y": 430, "width": 140 }] },
          { "pose": "sit_rear", "image": "acre:image/camp_alaron_sit_rear", "anchorX": 200, "anchorY": 600, "facing": "Away" } ] }]
        """;

    private const string Activity = """
        [{ "id": "acre:campactivity/cooking", "name": "Cooking", "skill": "acre:skill/12", "description": "A meal from what's carried." }]
        """;

    private const string Talk = """
        [{ "id": "acre:camptalk/brenna_home", "script": "acre:dialogue/brenna_home", "members": ["aidyn:entity/0x0243"],
           "flags": [{ "flag": "aidyn:flag/458" }], "done": "aidyn:flag/5900", "weight": 2 }]
        """;

    private static PluginSet Load(TempPlugins temp, params (string File, string Json)[] more) => PluginSet.Load([
        temp.Add("aidyn", null, ("flags.json", """[{ "id": "aidyn:flag/458", "label": "After Shamsuk" }, { "id": "aidyn:flag/5900", "label": "Told" }]""")),
        temp.Add("acre", ["aidyn"], [("camp/campsites.json", Campsite), ("camp/figures.json", Figure), ("camp/activities.json", Activity), ("camp/talks.json", Talk), .. more]),
    ]);

    [Fact]
    public void TheCampRecordsLoadAndValidateAndTheCampsiteIsChosenByTerrain()
    {
        using var temp = new TempPlugins();
        var set = Load(temp);
        Assert.Equal([], set.Validate().Problems.Select(p => p.ToString()).ToList());

        var forest = set.Get<CampsiteRecord>(RecordId.Parse("acre:campsite/forest"));
        Assert.Equal(1672, forest.Width);
        Assert.Equal(0.6f, forest.Fire.Y);
        Assert.Equal(0.58f, forest.Pot!.Y);
        Assert.Equal(0.26f, forest.SafeUi!.Height);
        var campfire = forest.Campfire!;
        Assert.Equal((RecordId.Parse("acre:image/camp_campfire"), 0.505f, 0.75f, 184f), (campfire.Image, campfire.Anchor.X, campfire.Anchor.Y, campfire.Width));
        Assert.Equal(RecordId.Parse("acre:image/camp_pot"), campfire.Pot);
        Assert.Equal(0.475f, campfire.Rim!.Y);
        Assert.Equal(RecordId.Parse("acre:image/camp_rock_front"), campfire.Front);
        Assert.Equal(["left_profile", "rear_center"], forest.Slots.Select(s => s.Id));
        Assert.Equal(CampFacing.Right, forest.Slot("left_profile")!.Facing);
        Assert.True(forest.Slot("left_profile")!.Mirror);
        Assert.False(forest.Slot("rear_center")!.Mirror);
        Assert.Equal([1, 2], forest.Layouts.Select(l => l.Size));
        Assert.False(forest.Calibrated);

        var snow = set.Get<CampsiteRecord>(RecordId.Parse("acre:campsite/snow"));
        Assert.Equal(new Rgba(200, 210, 255, 255), snow.Ambient);
        Assert.Null(snow.Campfire);
        Assert.True(snow.Calibrated);

        Assert.Equal(230, forest.FigureHeight);
        Assert.Equal(0.3055f, forest.Horizon);
        Assert.Equal(new CampSun(0.55f, 0.5f, -0.67f, new Rgba(255, 214, 150, 255), 0.85f), forest.Sun);
        Assert.Null(snow.Sun);
        Assert.Equal(0.35f, forest.LightAt(12).Fire);
        Assert.Null(forest.LightAt(22).Sun);
        Assert.Equal(1, forest.LightAt(22).Fire);
        Assert.Null(snow.Lights);
        Assert.Equal(0, snow.FigureHeight);
        Assert.Equal(0, snow.Horizon);

        Assert.Equal(forest.Id, CampsiteRecord.For(set, 3)!.Id);
        Assert.Equal(snow.Id, CampsiteRecord.For(set, 8)!.Id);
        // A terrain neither names takes the first with no terrains of its own, and there's none, so the first of all.
        Assert.Equal(forest.Id, CampsiteRecord.For(set, 27)!.Id);
        Assert.Equal(forest.Id, CampsiteRecord.For(set, null)!.Id);

        var alaron = set.Get<CampFigureRecord>(RecordId.Parse("acre:campfigure/alaron"));
        Assert.Equal(RecordId.Hex(RecordTypes.Entity, 0x023F), alaron.Entity);
        Assert.Equal(718, alaron.Height);
        Assert.Equal([CampFacing.Right], alaron.PoseOf(CampPoses.SitProfile)!.Facings);
        // A pose painted both ways is two images, keyed by the pose and the facing.
        Assert.Equal(RecordId.Parse("acre:image/camp_alaron_sit_profile_left"), alaron.PoseOf(CampPoses.SitProfile, CampFacing.Left)!.Image);
        Assert.Equal(RecordId.Parse("acre:image/camp_alaron_sit_profile_right_normal"), alaron.PoseOf(CampPoses.SitProfile, CampFacing.Right)!.Normal);
        Assert.Null(alaron.PoseOf(CampPoses.SitProfile, CampFacing.Away));
        Assert.Null(alaron.PoseOf(CampPoses.TendFire));
        // Each foot's sole, for its shadow; a pose may name none.
        Assert.Equal([new CampFoot(72, 444, 140), new CampFoot(94, 430, 140)], alaron.PoseOf(CampPoses.SitProfile, CampFacing.Left)!.Feet);
        Assert.Null(alaron.PoseOf(CampPoses.SitProfile, CampFacing.Right)!.Feet);

        var cooking = Assert.Single(set.All<CampActivityRecord>(RecordTypes.CampActivity));
        Assert.Equal("acre:skill/12", cooking.Skill.ToString());

        var talk = Assert.Single(set.All<CampTalkRecord>(RecordTypes.CampTalk));
        Assert.Equal(["aidyn:entity/0x0243"], talk.Members!.Select(m => m.ToString()));
        Assert.Equal(RecordId.Decimal(RecordTypes.Flag, 5900), talk.Done);
        Assert.Equal(2, talk.Weight);
        Assert.Null(talk.Terrains);
    }

    [Fact]
    public void AModPatchesASlotAndAPoseByTheirKeys()
    {
        using var temp = new TempPlugins();
        var set = PluginSet.Load([
            temp.Add("aidyn", null),
            temp.Add("acre", ["aidyn"], ("camp/campsites.json", Campsite), ("camp/figures.json", Figure)),
            temp.Add("mymod", ["acre"],
                ("site.json", """{ "patch": "acre:campsite/forest", "slots": [{ "id": "rear_center", "scale": 0.9 }], "calibrated": true }"""),
                ("figure.json", """{ "patch": "acre:campfigure/alaron", "poses": [{ "pose": "sit_rear", "facing": "Away", "anchorX": 210 }, { "pose": "sit_profile", "facing": "Left", "anchorY": 436 }] }""")),
        ]);
        Assert.Empty(set.Validate().Problems);
        var forest = set.Get<CampsiteRecord>(RecordId.Parse("acre:campsite/forest"));
        Assert.Equal(0.9f, forest.Slot("rear_center")!.Scale);
        Assert.Equal(10, forest.Slot("rear_center")!.Depth);
        Assert.Equal(0.95f, forest.Slot("left_profile")!.Scale);
        Assert.True(forest.Calibrated);
        var alaron = set.Get<CampFigureRecord>(RecordId.Parse("acre:campfigure/alaron"));
        Assert.Equal(210, alaron.PoseOf(CampPoses.SitRear)!.AnchorX);
        Assert.Equal(600, alaron.PoseOf(CampPoses.SitRear)!.AnchorY);
        // The pose facing left is patched; the same pose facing right isn't.
        Assert.Equal(436, alaron.PoseOf(CampPoses.SitProfile, CampFacing.Left)!.AnchorY);
        Assert.Equal(440, alaron.PoseOf(CampPoses.SitProfile, CampFacing.Right)!.AnchorY);
        Assert.Equal(3, alaron.Poses.Count);
    }
}
