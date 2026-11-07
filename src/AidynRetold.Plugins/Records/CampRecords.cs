namespace AidynRetold.Plugins.Records;

/// <summary>
/// Something the party can do at camp before it rests, the remade game's (<c>acre:campactivity/&lt;name&gt;</c>, the user's design,
/// Nov 7, 2026): cooking, brewing, making armor. Each is a skill whose recipes the camp screen offers, as the Party tab's popups
/// do, and each thing made adds its hours to the rest, so a long evening's cooking is a late start.
/// </summary>
/// <param name="Name">What the camp screen calls it.</param>
/// <param name="Skill">The skill whose recipes it offers (<c>aidyn:skill/n</c>, or the Cook's <c>acre:skill/12</c>), done by the member best at it.</param>
/// <param name="Description">A line under its name, if any.</param>
/// <param name="Order">Where it comes in the camp screen's list; ties by ID.</param>
public sealed record CampActivityRecord(RecordId Id, string Name, RecordId Skill, string? Description = null, int Order = 0);

/// <summary>Which way a figure faces on the screen: toward the left or right edge, or away from the viewer, into the picture.</summary>
public enum CampFacing
{
    Left,
    Right,
    Away,
}

/// <summary>
/// The seven canonical poses a figure at camp may have (the campsite brief's table), which slots name and figures' images are
/// keyed by. A library to grow into, not a requirement: a figure declares the poses it has.
/// </summary>
public static class CampPoses
{
    /// <summary>Relaxed standing, a strict profile toward the fire.</summary>
    public const string StandProfile = "stand_profile";
    /// <summary>Seated, a strict profile toward the fire.</summary>
    public const string SitProfile = "sit_profile";
    /// <summary>Standing, turned toward the fire with the face mostly hidden.</summary>
    public const string StandRear3Q = "stand_rear_3q";
    /// <summary>Seated, turned toward the fire with the face mostly hidden.</summary>
    public const string SitRear3Q = "sit_rear_3q";
    /// <summary>Standing with the back to the viewer.</summary>
    public const string StandRear = "stand_rear";
    /// <summary>Seated with the back to the viewer.</summary>
    public const string SitRear = "sit_rear";
    /// <summary>Crouched or leaning to tend the fire, the hands toward the shared pot.</summary>
    public const string TendFire = "tend_fire";

    public static readonly IReadOnlyList<string> All = [StandProfile, SitProfile, StandRear3Q, SitRear3Q, StandRear, SitRear, TendFire];

    /// <summary>Whether a pose is one of the seven.</summary>
    public static bool IsKnown(string pose) => All.Contains(pose);

    /// <summary>Whether the figure sits in the pose, so its apparent height is a seated one's, never scaled to a standing one's.</summary>
    public static bool IsSeated(string pose) => pose is SitProfile or SitRear3Q or SitRear or TendFire;

    /// <summary>The facings a pose can be drawn in: a profile faces left or right, and the rest away.</summary>
    public static IReadOnlyList<CampFacing> FacingsOf(string pose) =>
        pose is StandProfile or SitProfile ? [CampFacing.Left, CampFacing.Right] : [CampFacing.Away];
}

/// <summary>A point in a campsite's picture, as a share of its width and height, 0 to 1.</summary>
public sealed record CampPoint(float X, float Y);

/// <summary>
/// A piece laid over a campsite's picture at one of its anchors, the user's art (Nov 7, 2026): the fire pit at the fire, the tripod at
/// the pot. <paramref name="Anchor"/> is the point of the image, as shares of its width and height, that sits on the anchor, and
/// <paramref name="Width"/> how wide the whole image is drawn, in the picture's pixels, its height kept to its shape.
/// </summary>
public sealed record CampProp(RecordId Image, CampPoint Anchor, float Width);

/// <summary>A rectangle in a campsite's picture, as shares of its width and height, 0 to 1.</summary>
public sealed record CampRect(float X, float Y, float Width, float Height);

/// <summary>
/// A place a figure can stand or sit at a campsite: a ground anchor with what it takes. A slot can take several poses, and a
/// layout names which slots a party of its size uses; the seven canonical poses aren't seven fixed seats.
/// </summary>
/// <param name="Id">A name, such as <c>left_profile</c> or <c>rear_center</c>, which layouts name it by.</param>
/// <param name="X">The ground-contact anchor, as a share of the picture's width.</param>
/// <param name="Y">The ground-contact anchor, as a share of the picture's height.</param>
/// <param name="Scale">How large a figure stands here for the camera's perspective, 1 at the fire's own depth.</param>
/// <param name="Depth">Draw order: a lower number is further from the viewer and drawn first. The fire's rear layers are at 0, its front at 100.</param>
/// <param name="Poses">The poses it takes (<see cref="CampPoses"/>), in order of preference.</param>
/// <param name="Facing">The way a figure here faces, so it looks toward the fire or its companions, never the viewer.</param>
/// <param name="Mirror">Whether a figure may be mirrored to face this way, where its own image faces the other and says it can be.</param>
/// <param name="Interaction">Where the figure's hands or tool reach, for <see cref="CampPoses.TendFire"/>: the pot, normally.</param>
public sealed record CampSlot(string Id, float X, float Y, float Scale, int Depth, IReadOnlyList<string> Poses, CampFacing Facing,
    bool Mirror = false, CampPoint? Interaction = null);

/// <summary>A curated arrangement for a party of <paramref name="Size"/>: which slots it uses, one a member. A campsite may give several a size.</summary>
public sealed record CampLayout(int Size, IReadOnlyList<string> Slots);

/// <summary>
/// A campsite the camp screen shows, the remade game's (<c>acre:campsite/&lt;biome&gt;</c>, the user's design, Nov 7, 2026): a
/// painted picture, its fire, where figures may stand or sit and which of those a party of each size uses. Every campsite keeps
/// the forest master's camera, fire anchor and ground plane, so one library of figures sits at all of them. Positions are shares of
/// the picture, 0 to 1, so the screen maps them into wherever the picture is drawn, letterboxed or not.
/// </summary>
/// <param name="Name">The biome's name: forest, snow, desert, marsh.</param>
/// <param name="Image">The picture (<c>acre:image/…</c>), drawn whole, never retouched by the game.</param>
/// <param name="Width">The picture's width in pixels, which anchors were measured on.</param>
/// <param name="Height">The picture's height in pixels.</param>
/// <param name="Fire">The fire's base, which every biome's picture keeps in the same place.</param>
/// <param name="Slots">The candidate anchors, at least seven for a party of up to five to vary over.</param>
/// <param name="Layouts">The curated layouts, by party size; a size with none falls back to the slots in order.</param>
/// <param name="Pot">The cooking pot, where a fire-tending figure's hands reach, if the picture has one.</param>
/// <param name="SafeUi">Where the menu may be drawn without covering the fire or the figures.</param>
/// <param name="Terrains">The terrains (<c>aidyn:terrain/n</c>'s indices) this campsite is shown on; a campsite with none is the fallback for the rest.</param>
/// <param name="Ambient">A tint laid over the figures for the biome's light, restrained; none leaves them as painted.</param>
/// <param name="Calibrated">
/// Whether the anchors were measured on the picture in the engine, rather than written as placeholders. The camp screen's debug
/// overlay says so while they aren't.
/// </param>
/// <param name="Vignette">
/// A transparent image laid over the picture as painted (<c>acre:image/…</c>), at the picture's size: its dark at its own alpha,
/// clear where the picture shows as it is. The screen changes nothing of it; none leaves the picture as it is.
/// </param>
/// <param name="Sound">
/// The loop played while the camp shows (<c>acre:sequence/…</c>, a recording as a sequence's <c>.wav</c>): the fire crackling, the
/// user's choice (Nov 7, 2026), in the ambient sounds' volume, fading in as the camp opens and out as it closes.
/// </param>
/// <param name="SoundVolume">How loud the loop plays, 0 to 1, under the ambient sounds' volume.</param>
/// <param name="Music">
/// The music while the camp shows, in the music's volume; none is silence. Trek's music stops as the camp opens, the user's choice
/// (Nov 7, 2026), and comes back as it closes; the camp gets music of its own later.
/// </param>
/// <param name="FirePit">
/// The fire pit, its stones and logs, laid over the picture with its flames' middle on <paramref name="Fire"/>, under the screen's own
/// flames, for a picture with no fire painted in.
/// </param>
/// <param name="Tripod">The tripod and its pot, laid over the picture with the pot's middle on <paramref name="Pot"/>, under the flames.</param>
public sealed record CampsiteRecord(
    RecordId Id,
    string Name,
    RecordId Image,
    int Width,
    int Height,
    CampPoint Fire,
    [property: KeyedList(nameof(CampSlot.Id))] IReadOnlyList<CampSlot> Slots,
    IReadOnlyList<CampLayout> Layouts,
    CampPoint? Pot = null,
    CampRect? SafeUi = null,
    IReadOnlyList<int>? Terrains = null,
    Rgba? Ambient = null,
    bool Calibrated = false,
    RecordId? Vignette = null,
    RecordId? Sound = null,
    float SoundVolume = 1,
    RecordId? Music = null,
    CampProp? FirePit = null,
    CampProp? Tripod = null)
{
    /// <summary>The depth the fire stands at among the slots': a slot under it is drawn behind the pit and the tripod, one over it in front.</summary>
    public const int FireDepth = 100;

    /// <summary>A slot by its name, or null.</summary>
    public CampSlot? Slot(string id) => Slots.FirstOrDefault(s => s.Id == id);

    /// <summary>
    /// The campsite for a terrain: the first whose <see cref="Terrains"/> name it, else the first with none, else the first of all,
    /// in ID order; null where the plugins have none, as the original game alone has none.
    /// </summary>
    public static CampsiteRecord? For(PluginSet plugins, int? terrain)
    {
        var sites = plugins.All<CampsiteRecord>(RecordTypes.Campsite).OrderBy(s => s.Id.ToString(), StringComparer.Ordinal).ToList();
        return (terrain is { } t ? sites.FirstOrDefault(s => s.Terrains?.Contains(t) == true) : null)
            ?? sites.FirstOrDefault(s => s.Terrains is null or { Count: 0 })
            ?? sites.FirstOrDefault();
    }
}

/// <summary>
/// One of a figure's poses: its image and where its feet or seat touch the ground in it. The image is the figure as painted, neutral,
/// with no biome's light baked in; the campsite's ambient and the fire's light are laid over it.
/// </summary>
/// <param name="Pose">Which pose (<see cref="CampPoses"/>).</param>
/// <param name="Image">The image (<c>acre:image/…</c>), with real alpha, cropped to the figure.</param>
/// <param name="AnchorX">The ground-contact point in the image's pixels, from its left.</param>
/// <param name="AnchorY">The ground-contact point in the image's pixels, from its top.</param>
/// <param name="Facing">The way the image faces as painted.</param>
/// <param name="Mirror">Whether it may be mirrored to face the other way: not with an asymmetric pauldron, emblem or weapon.</param>
/// <param name="Normal">A normal map aligned to the image, if one proved itself; none draws without.</param>
/// <param name="LightMask">A directional light mask for the fire's side, the simpler alternative to a normal map.</param>
/// <param name="HandX">Where the hands or tool reach in the image's pixels, for <see cref="CampPoses.TendFire"/>.</param>
/// <param name="HandY">As <paramref name="HandX"/>, from the top.</param>
public sealed record CampPose(string Pose, RecordId Image, int AnchorX, int AnchorY, CampFacing Facing, bool Mirror = false,
    RecordId? Normal = null, RecordId? LightMask = null, int? HandX = null, int? HandY = null)
{
    /// <summary>The facings this image can show: its own, and the other if it may be mirrored (away stays away).</summary>
    public IEnumerable<CampFacing> Facings => Mirror && Facing != CampFacing.Away ? [Facing, Facing == CampFacing.Left ? CampFacing.Right : CampFacing.Left] : [Facing];
}

/// <summary>
/// The figure a character cuts at camp (<c>acre:campfigure/&lt;name&gt;</c>): its poses' images and its stature. A character with
/// no figure is placed all the same, and the camp screen says it has nothing to draw.
/// </summary>
/// <param name="Entity">Whose figure it is.</param>
/// <param name="Stature">The character's height against the one canonical height, 1: a big man above it, a child below.</param>
/// <param name="Poses">The poses it has, each once.</param>
public sealed record CampFigureRecord(
    RecordId Id,
    RecordId Entity,
    [property: KeyedList(nameof(CampPose.Pose))] IReadOnlyList<CampPose> Poses,
    float Stature = 1)
{
    /// <summary>The figure's image for a pose, or null if it hasn't one.</summary>
    public CampPose? PoseOf(string pose) => Poses.FirstOrDefault(p => p.Pose == pose);
}

/// <summary>
/// A conversation the party may have at camp, the remade game's (<c>acre:camptalk/&lt;name&gt;</c>, the user's design, Nov 7, 2026):
/// a script shown in the conversation box over the camp screen as the party rests, chosen among those whose conditions hold. The
/// original's eight story camp scenes come first; these fill the nights between.
/// </summary>
/// <param name="Script">The conversation (<c>DialogueMode.Conversation</c>).</param>
/// <param name="Members">Members who must all be in the party.</param>
/// <param name="Anyone">Members of whom at least one must be in the party.</param>
/// <param name="Flags">Conditions on the flags that must all hold.</param>
/// <param name="Terrains">The terrains it's told on; none is anywhere.</param>
/// <param name="Done">A flag set once it has been told, after which it isn't again; none tells it again.</param>
/// <param name="Weight">How likely it is against the others that could be told, 1 by default.</param>
public sealed record CampTalkRecord(
    RecordId Id,
    RecordId Script,
    IReadOnlyList<RecordId>? Members = null,
    IReadOnlyList<RecordId>? Anyone = null,
    IReadOnlyList<FlagGate>? Flags = null,
    IReadOnlyList<int>? Terrains = null,
    RecordId? Done = null,
    int Weight = 1);
