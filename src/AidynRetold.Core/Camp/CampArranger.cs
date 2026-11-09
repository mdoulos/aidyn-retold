using AidynRetold.Core.Rng;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Camp;

/// <summary>Where one member sits or stands at camp, in which pose, and what draws it.</summary>
/// <param name="Member">The member's entity.</param>
/// <param name="Slot">The campsite's slot it's at.</param>
/// <param name="Pose">The pose it takes (<see cref="CampPoses"/>).</param>
/// <param name="Facing">The way it faces: the slot's.</param>
/// <param name="Mirrored">Whether its image is drawn mirrored to face that way.</param>
/// <param name="Figure">The member's figure, or null when the plugins have none for it.</param>
/// <param name="Image">The pose's image, or null when the figure hasn't it: the screen then has nothing to draw and says so.</param>
/// <param name="Clip">Which of its model's poses of the kind it takes (<see cref="CampFigureModel.Poses"/>), where it's drawn from a model that has several.</param>
/// <param name="Lean">How far toward that pose's upright version it sits, 0 to 1 (<see cref="CampModelPose.Upright"/>).</param>
public sealed record CampPlacement(RecordId Member, CampSlot Slot, string Pose, CampFacing Facing, bool Mirrored, CampFigureRecord? Figure,
    CampPose? Image, int Clip = 0, float Lean = 0)
{
    public bool Drawable => Image is not null;

    /// <summary>How large the figure is drawn: the slot's perspective scale times the figure's stature.</summary>
    public float Scale => Slot.Scale * (Figure?.Stature ?? 1);

    /// <summary>
    /// How many of the picture's pixels one of the image's is drawn over (the campsite brief's section 4): the scale, times the campsite's
    /// one canonical height over the figure's own height in its images, where both are given, else 1, the images being painted at the
    /// picture's scale.
    /// </summary>
    public float PictureScale(CampsiteRecord site) =>
        Scale * (site.FigureHeight > 0 && Figure is { Height: > 0 } figure ? site.FigureHeight / figure.Height : 1);
}

/// <summary>What the camp's debug Poses button's list does to the figures, the user's (Nov 9, 2026).</summary>
public enum CampPoseChange
{
    /// <summary>New poses, and new places for them.</summary>
    Random,

    /// <summary>Each to its next numbered pose of the kind it's in.</summary>
    Next,

    /// <summary>Each to another pose of the kind it's in, at random.</summary>
    SameKind,

    /// <summary>Those standing sit, and those sitting stand.</summary>
    Swap,
}

/// <summary>A party's arrangement at a campsite: a layout, each member at one slot, and what the arranging had to say.</summary>
/// <param name="Fallback">Whether no layout took the party and the known-safe fallback was used, with a diagnostic saying why.</param>
public sealed record CampArrangement(CampsiteRecord Site, CampLayout Layout, IReadOnlyList<CampPlacement> Placements,
    IReadOnlyList<string> Diagnostics, bool Fallback)
{
    /// <summary>The placements as they're drawn: the furthest first, by the slot's depth and then its height in the picture.</summary>
    public IEnumerable<CampPlacement> InDrawOrder => Placements.OrderBy(p => p.Slot.Depth).ThenBy(p => p.Slot.Y);
}

/// <summary>
/// Puts the party round the fire (the campsite brief's section 5 and 11): a curated layout for the party's size, chosen by the
/// camp's seed, each member at one of its slots in a pose the slot takes and the member's figure has, facing the slot's way,
/// mirrored only where both allow it. Nothing is placed twice, hidden, stretched or invented: a member with no figure is placed
/// with nothing to draw, and a party no layout takes gets the known-safe fallback, each with a diagnostic.
/// </summary>
public static class CampArranger
{
    /// <summary>The most members a campsite is laid out for.</summary>
    public const int MostMembers = 5;

    /// <summary>The most slots the figures shown in place of the party take: all six round the fire, the user's (Nov 9, 2026).</summary>
    public const int MostPreviewed = 6;

    /// <summary>The slots a campsite should give, for a party of up to five to vary over.</summary>
    public const int SlotsWanted = 7;

    /// <param name="party">The active members, each once, in party order.</param>
    /// <param name="figures">Each member's figure, or null for one the plugins have none for.</param>
    /// <param name="seed">The camp's seed (<see cref="CampSeed"/>).</param>
    /// <param name="avoid">The last camp's layout, which another is preferred to when the size has more than one.</param>
    public static CampArrangement Arrange(CampsiteRecord site, IReadOnlyList<RecordId> party, Func<RecordId, CampFigureRecord?> figures, uint seed,
        CampLayout? avoid = null)
    {
        var notes = new List<string>();
        var members = party.Distinct().ToList();
        if (members.Count != party.Count)
            notes.Add("A member was listed twice; each is placed once.");
        if (members.Count == 0)
            throw new ArgumentException("A camp needs at least one member.", nameof(party));
        if (members.Count > MostMembers)
        {
            notes.Add($"{members.Count} members, where a campsite is laid out for at most {MostMembers}; the first {MostMembers} are placed.");
            members = members.Take(MostMembers).ToList();
        }
        if (site.Slots.Count < SlotsWanted)
            notes.Add($"{site.Id} has {site.Slots.Count} slots, fewer than the {SlotsWanted} wanted for a party of up to {MostMembers} to vary over.");

        var size = members.Count;
        var candidates = new List<CampLayout>();
        foreach (var layout in site.Layouts.Where(l => l.Size == size))
        {
            if (layout.Slots.Count != size || layout.Slots.Distinct().Count() != size)
                notes.Add($"{site.Id}'s layout for {size} names {layout.Slots.Count} slots ({string.Join(", ", layout.Slots)}); it needs {size} distinct ones.");
            else if (layout.Slots.FirstOrDefault(id => site.Slot(id) is null) is { } missing)
                notes.Add($"{site.Id}'s layout for {size} names slot {missing}, which the campsite hasn't.");
            else
                candidates.Add(layout);
        }

        var random = new Lcg(seed);
        var figureOf = new Dictionary<RecordId, CampFigureRecord?>();
        foreach (var member in members)
        {
            figureOf[member] = figures(member);
            if (figureOf[member] is null)
                notes.Add($"No figure for {member}; it's placed with nothing to draw.");
        }

        if (candidates.Count > 0)
        {
            // Start where the seed says and go round; the last camp's layout, if another is to be had, comes last.
            var start = (int)random.Below((uint)candidates.Count);
            var order = Enumerable.Range(0, candidates.Count).Select(i => candidates[(start + i) % candidates.Count]).ToList();
            if (avoid is not null && order.Count > 1 && order.FirstOrDefault(l => l.Slots.SequenceEqual(avoid.Slots)) is { } last)
            {
                order.Remove(last);
                order.Add(last);
            }
            foreach (var layout in order)
            {
                var shuffled = members.ToList();
                Shuffle(shuffled, random);
                var placements = new CampPlacement?[size];
                if (Assign(site, layout, shuffled, figureOf, random, 0, placements))
                    return new CampArrangement(site, layout, [.. placements!], notes, Fallback: false);
            }
            notes.Add($"No layout for {size} at {site.Id} takes this party's figures as they face; the fallback is used.");
        }
        else
        {
            notes.Add($"{site.Id} has no layout for a party of {size}; the fallback is used.");
        }

        // The known-safe fallback: the first layout of the size, or the nearest slots in order, each member in party order, in the
        // first pose the slot takes, drawn only where the figure has it facing the slot's way.
        var fallback = candidates.FirstOrDefault()
            ?? new CampLayout(size, [.. site.Slots.OrderByDescending(s => s.Depth).ThenBy(s => s.X).Take(size).Select(s => s.Id)]);
        if (fallback.Slots.Count < size)
            notes.Add($"{site.Id} has only {site.Slots.Count} slots for a party of {size}; the rest aren't placed.");
        var placed = new List<CampPlacement>();
        foreach (var (member, id) in members.Zip(fallback.Slots))
        {
            var slot = site.Slot(id)!;
            var figure = figureOf[member];
            var pose = slot.Poses.FirstOrDefault(CampPoses.IsKnown) ?? slot.Poses.FirstOrDefault() ?? CampPoses.SitRear;
            var fit = figure is null ? null : Fit(figure, slot, random, prefer: pose);
            placed.Add(fit is var (p, mirrored, image) && image is not null
                ? Varied(new CampPlacement(member, slot, p, slot.Facing, mirrored, figure, image), random)
                : new CampPlacement(member, slot, pose, slot.Facing, false, figure, null));
        }
        return new CampArrangement(site, fallback, placed, notes, Fallback: true);
    }

    /// <summary>Whether a figure can take a slot: it has a pose the slot takes, facing the slot's way as painted or mirrored where both allow.</summary>
    public static bool Fits(CampFigureRecord figure, CampSlot slot) => Options(figure, slot).Any();

    private static bool Assign(CampsiteRecord site, CampLayout layout, IReadOnlyList<RecordId> members, IReadOnlyDictionary<RecordId, CampFigureRecord?> figures,
        IRandom random, int index, CampPlacement?[] placements)
    {
        if (index == members.Count)
            return true;
        var member = members[index];
        var figure = figures[member];
        // The free slots, tried from where the seed says, so who sits where varies with the day.
        var free = layout.Slots.Where((_, i) => placements[i] is null).Select(id => site.Slot(id)!).ToList();
        var start = (int)random.Below((uint)free.Count);
        for (var k = 0; k < free.Count; k++)
        {
            var slot = free[(start + k) % free.Count];
            var at = layout.Slots.ToList().IndexOf(slot.Id);
            if (figure is null)
            {
                var pose = slot.Poses.FirstOrDefault(CampPoses.IsKnown) ?? slot.Poses.FirstOrDefault() ?? CampPoses.SitRear;
                placements[at] = new CampPlacement(member, slot, pose, slot.Facing, false, null, null);
            }
            else if (Fit(figure, slot, random) is var (pose, mirrored, image))
            {
                placements[at] = Varied(new CampPlacement(member, slot, pose, slot.Facing, mirrored, figure, image), random);
            }
            else
            {
                continue;
            }
            if (Assign(site, layout, members, figures, random, index + 1, placements))
                return true;
            placements[at] = null;
        }
        return false;
    }

    /// <summary>
    /// The poses a figure can take at a slot, in the slot's order of preference, each once: its image painted facing the slot's way, else
    /// one mirrored to face it where both allow.
    /// </summary>
    private static IEnumerable<(string Pose, bool Mirrored, CampPose Image)> Options(CampFigureRecord figure, CampSlot slot)
    {
        foreach (var pose in slot.Poses)
        {
            if (figure.PoseOf(pose, slot.Facing) is { } painted)
                yield return (pose, false, painted);
            else if (slot.Mirror && figure.Poses.FirstOrDefault(p => p.Pose == pose && p.Mirror && p.Facings.Contains(slot.Facing)) is { } flipped)
                yield return (pose, true, flipped);
        }
    }

    /// <summary>
    /// Figures in place of the party, for judging their art round the fire (<c>--camp-figure</c>, a launch's): <see cref="MostPreviewed"/> of them,
    /// at that many of the slots one of them fits, the user's (Nov 9, 2026), or at all of those where there are fewer. The seed chooses which
    /// slots, who takes each and which of its poses there, a figure drawn from a model taking a different one of its poses at each slot while it has one left. Who's chosen among those least placed so far that
    /// fit, the slots fewest fit going first, so the figures are shared out as evenly as their art allows, however few there are.
    /// </summary>
    public static CampArrangement Preview(CampsiteRecord site, IReadOnlyList<CampFigureRecord> figures, uint seed) => Preview(site, figures, new Lcg(seed));

    /// <summary>One figure at as many as <see cref="MostMembers"/> of the slots it fits.</summary>
    public static CampArrangement Preview(CampsiteRecord site, CampFigureRecord figure, uint seed) => Preview(site, [figure], seed);

    private static CampArrangement Preview(CampsiteRecord site, IReadOnlyList<CampFigureRecord> figures, IRandom random)
    {
        var slots = site.Slots.Where(s => figures.Any(f => Fits(f, s))).ToList();
        Shuffle(slots, random);
        var placements = new List<CampPlacement>();
        var decks = new Dictionary<CampFigureRecord, List<(bool Seated, int Clip)>>();
        foreach (var slot in slots.Take(MostPreviewed).OrderBy(s => figures.Count(f => Fits(f, s))))
        {
            var fitting = figures.Where(f => Fits(f, slot)).ToList();
            var fewest = fitting.Min(f => placements.Count(p => p.Figure == f));
            var least = fitting.Where(f => placements.Count(p => p.Figure == f) == fewest).ToList();
            var figure = least[(int)random.Below((uint)least.Count)];
            if (Dealt(figure, slot, decks, random) is { } dealt)
                placements.Add(dealt);
            else if (Fit(figure, slot, random) is var (pose, mirrored, image))
                placements.Add(Varied(new CampPlacement(figure.Entity, slot, pose, slot.Facing, mirrored, figure, image), random));
        }
        placements = [.. placements.OrderBy(p => site.Slots.ToList().FindIndex(s => s.Id == p.Slot.Id))];
        List<string> notes = placements.Count == 0 ? [$"{string.Join(", ", figures.Select(f => f.Id))} fits none of {site.Id}'s slots."] : [];
        return new CampArrangement(site, new CampLayout(placements.Count, [.. placements.Select(p => p.Slot.Id)]), placements, notes, Fallback: false);
    }

    /// <summary>
    /// The figures shown in place of the party drawn afresh (<see cref="Preview(CampsiteRecord, IReadOnlyList{CampFigureRecord}, uint)"/>):
    /// which slots they fill, who's at each and how, the debug Poses button's, the user's (Nov 8, 2026). Drawn again until everyone is doing
    /// something else, the user's (Nov 9, 2026), or failing that until something has changed, where something can.
    /// </summary>
    public static CampArrangement Reshuffle(CampArrangement arrangement, IReadOnlyList<CampFigureRecord> figures, IRandom random)
    {
        const int Tries = 32;
        var was = arrangement.Placements.ToDictionary(p => p.Slot.Id, Shown);
        var next = arrangement;
        CampArrangement? changed = null;
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            next = Preview(arrangement.Site, figures, random);
            var fresh = next.Placements.Count(p => !was.TryGetValue(p.Slot.Id, out var before) || before != Shown(p));
            if (fresh == next.Placements.Count && fresh > 0)
                return next;
            if (fresh > 0)
                changed ??= next;
        }
        return changed ?? next;

        static (RecordId Member, string Pose, int Clip) Shown(CampPlacement p) => (p.Member, p.Pose, p.Clip);
    }

    /// <summary>
    /// The pose a figure takes at a slot standing or seated (<see cref="CampPoses.IsSeated"/>): the first such the slot takes and the figure
    /// has facing its way, as painted or mirrored where both allow; null with none.
    /// </summary>
    public static (string Pose, bool Mirrored, CampPose Image)? Pick(CampFigureRecord figure, CampSlot slot, bool seated) =>
        Options(figure, slot).Where(o => CampPoses.IsSeated(o.Pose) == seated).Select(o => ((string, bool, CampPose)?)o).FirstOrDefault();

    /// <summary>
    /// The arrangement with a slot given to a figure, standing or seated (<see cref="Pick"/>), in place of whoever was there, or emptied
    /// with none: the debug overlay's choice for each slot, the user's (Nov 7, 2026). The layout is the slots then taken, in the campsite's
    /// order. Unchanged where the figure has no such pose there.
    /// </summary>
    public static CampArrangement Place(CampArrangement arrangement, string slotId, CampFigureRecord? figure, bool seated)
    {
        var site = arrangement.Site;
        var slot = site.Slot(slotId) ?? throw new ArgumentException($"{site.Id} has no slot {slotId}.", nameof(slotId));
        var placements = arrangement.Placements.Where(p => p.Slot.Id != slotId).ToList();
        if (figure is not null)
        {
            if (Pick(figure, slot, seated) is not var (pose, mirrored, image))
                return arrangement;
            placements.Add(new CampPlacement(figure.Entity, slot, pose, slot.Facing, mirrored, figure, image));
        }
        placements = [.. placements.OrderBy(p => site.Slots.ToList().FindIndex(s => s.Id == p.Slot.Id))];
        return arrangement with { Placements = placements, Layout = new CampLayout(placements.Count, [.. placements.Select(p => p.Slot.Id)]) };
    }

    /// <summary>
    /// Each figure moved on to the next numbered of its model's poses of the kind it's in, the first after the last, everyone where they are: one of the debug Poses button's,
    /// the user's (Nov 9, 2026). A figure with no model, or one pose of the kind, stays as it is.
    /// </summary>
    public static CampArrangement NextPoses(CampArrangement arrangement, IRandom random) =>
        arrangement with { Placements = [.. arrangement.Placements.Select(p => ModelPoses(p) is { Count: > 1 } poses ? Posed(p, (p.Clip + 1) % poses.Count, poses, random) : p)] };

    /// <summary>Each figure moved at random to another of its model's poses of the kind it's in, sitting if it was sitting and standing if it was standing, everyone where they are: another of the button's.</summary>
    public static CampArrangement OtherPoses(CampArrangement arrangement, IRandom random) =>
        Drawn(arrangement, random, p => ModelPoses(p) is { Count: > 1 } poses ? Posed(p, (p.Clip + 1 + (int)random.Below((uint)poses.Count - 1)) % poses.Count, poses, random) : p);

    /// <summary>Those standing sit and those sitting stand, each where it is, in a pose of the other kind chosen at random: another of the button's. A figure with no such pose at its slot stays as it is.</summary>
    public static CampArrangement SwapPoses(CampArrangement arrangement, IRandom random) =>
        Drawn(arrangement, random, p => p.Figure is { } figure && Pick(figure, p.Slot, !CampPoses.IsSeated(p.Pose)) is var (pose, mirrored, image)
            ? Varied(p with { Pose = pose, Mirrored = mirrored, Image = image }, random)
            : p);

    /// <summary>A change made to every placement, made again, while it can be, until no two of a modelled figure's placements of a kind are in the same pose.</summary>
    private static CampArrangement Drawn(CampArrangement arrangement, IRandom random, Func<CampPlacement, CampPlacement> change)
    {
        const int Tries = 32;
        List<CampPlacement> placements = [];
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            placements = [.. arrangement.Placements.Select(change)];
            if (placements.Where(p => p.Figure?.Model is not null).GroupBy(p => (p.Member, CampPoses.IsSeated(p.Pose))).All(same => same.Select(p => p.Clip).Distinct().Count() == same.Count()))
                break;
        }
        return arrangement with { Placements = placements };
    }

    /// <summary>The poses a placement's model has of the kind it's in, or null with no model.</summary>
    private static IReadOnlyList<CampModelPose>? ModelPoses(CampPlacement placement) => placement.Figure?.Model?.Poses(CampPoses.IsSeated(placement.Pose));

    private static CampPlacement Posed(CampPlacement placement, int clip, IReadOnlyList<CampModelPose> poses, IRandom random) =>
        placement with { Clip = clip, Lean = Lean(poses[clip], random) };

    /// <summary>One of a figure's options at a slot, chosen by the seed, or the preferred pose if it's among them; null with none.</summary>
    private static (string Pose, bool Mirrored, CampPose Image)? Fit(CampFigureRecord figure, CampSlot slot, IRandom random, string? prefer = null)
    {
        var options = Options(figure, slot).ToList();
        if (options.Count == 0)
            return null;
        if (prefer is not null && options.FirstOrDefault(o => o.Pose == prefer) is { Image: not null } preferred)
            return preferred;
        return options[(int)random.Below((uint)options.Count)];
    }

    /// <summary>
    /// A figure drawn from a model, placed in the next of its poses in a deck dealt out across the preview, so each slot's differs while there are any left: the first in the deck
    /// that the slot can take. Null for a figure with no model, or a slot that takes none of its poses.
    /// </summary>
    private static CampPlacement? Dealt(CampFigureRecord figure, CampSlot slot, Dictionary<CampFigureRecord, List<(bool Seated, int Clip)>> decks, IRandom random)
    {
        if (figure.Model is not { } model)
            return null;
        if (!decks.TryGetValue(figure, out var deck) || deck.Count == 0)
        {
            deck = [.. Enumerable.Range(0, model.Sits.Count).Select(i => (true, i)), .. Enumerable.Range(0, model.Stands.Count).Select(i => (false, i))];
            Shuffle(deck, random);
            decks[figure] = deck;
        }
        foreach (var card in deck)
        {
            if (Pick(figure, slot, card.Seated) is not var (pose, mirrored, image))
                continue;
            deck.Remove(card);
            return new CampPlacement(figure.Entity, slot, pose, slot.Facing, mirrored, figure, image, card.Clip, Lean(model.Poses(card.Seated)[card.Clip], random));
        }
        return null;
    }

    /// <summary>How upright a placement in a pose sits: a random share where the pose has an upright version, no number drawn where not.</summary>
    private static float Lean(CampModelPose pose, IRandom random) => pose.Upright is null ? 0 : random.Below(1001) / 1000f;

    /// <summary>A placement with its model's pose chosen by the seed, and how upright it sits if that pose can vary; no number is drawn where there's no choice.</summary>
    private static CampPlacement Varied(CampPlacement placement, IRandom random)
    {
        if (placement.Figure?.Model?.Poses(CampPoses.IsSeated(placement.Pose)) is not { Count: > 0 } poses)
            return placement;
        var clip = poses.Count > 1 ? (int)random.Below((uint)poses.Count) : 0;
        return placement with { Clip = clip, Lean = Lean(poses[clip], random) };
    }

    private static void Shuffle<T>(IList<T> list, IRandom random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = (int)random.Below((uint)(i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
