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
public sealed record CampPlacement(RecordId Member, CampSlot Slot, string Pose, CampFacing Facing, bool Mirrored, CampFigureRecord? Figure,
    CampPose? Image)
{
    public bool Drawable => Image is not null;

    /// <summary>How large the figure is drawn: the slot's perspective scale times the figure's stature.</summary>
    public float Scale => Slot.Scale * (Figure?.Stature ?? 1);
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
            var start = (int)(random.Next() % (uint)candidates.Count);
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
                ? new CampPlacement(member, slot, p, slot.Facing, mirrored, figure, image)
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
        var start = (int)(random.Next() % (uint)free.Count);
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
                placements[at] = new CampPlacement(member, slot, pose, slot.Facing, mirrored, figure, image);
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

    /// <summary>The poses a figure can take at a slot, with whether each is mirrored: as painted first, mirrored after, in the slot's order of preference.</summary>
    private static IEnumerable<(string Pose, bool Mirrored, CampPose Image)> Options(CampFigureRecord figure, CampSlot slot)
    {
        foreach (var pose in slot.Poses)
        {
            if (figure.PoseOf(pose) is not { } image)
                continue;
            if (image.Facing == slot.Facing)
                yield return (pose, false, image);
            else if (image.Mirror && slot.Mirror && image.Facings.Contains(slot.Facing))
                yield return (pose, true, image);
        }
    }

    /// <summary>One of a figure's options at a slot, chosen by the seed, or the preferred pose if it's among them; null with none.</summary>
    private static (string Pose, bool Mirrored, CampPose Image)? Fit(CampFigureRecord figure, CampSlot slot, IRandom random, string? prefer = null)
    {
        var options = Options(figure, slot).ToList();
        if (options.Count == 0)
            return null;
        if (prefer is not null && options.FirstOrDefault(o => o.Pose == prefer) is { Image: not null } preferred)
            return preferred;
        return options[(int)(random.Next() % (uint)options.Count)];
    }

    private static void Shuffle<T>(IList<T> list, IRandom random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = (int)(random.Next() % (uint)(i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
