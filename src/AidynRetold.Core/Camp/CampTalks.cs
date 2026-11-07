using AidynRetold.Core.Rng;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Camp;

/// <summary>
/// Which conversation the party has at camp, the remade game's (<see cref="CampTalkRecord"/>): among those whose members are in
/// the party, whose flags hold, that are told on this terrain and haven't been told once already, one by weight on the camp's seed.
/// The original's eight story scenes (<see cref="CampingRecord.Scenes"/>) are asked first by whoever camps; these fill the other nights.
/// </summary>
public static class CampTalks
{
    /// <param name="party">The active members' entities.</param>
    /// <param name="holds">Whether a flag condition holds (<c>FlagNetwork.Holds</c>).</param>
    /// <param name="isSet">Whether a flag is set, for the talks' done flags.</param>
    /// <param name="terrain">The terrain the party camps on, or null where it isn't known.</param>
    public static IEnumerable<CampTalkRecord> Eligible(IEnumerable<CampTalkRecord> talks, IReadOnlyCollection<RecordId> party,
        Func<FlagGate, bool> holds, Func<RecordId, bool> isSet, int? terrain) =>
        talks.Where(t =>
            (t.Members is null || t.Members.All(party.Contains))
            && (t.Anyone is null || t.Anyone.Count == 0 || t.Anyone.Any(party.Contains))
            && (t.Flags is null || t.Flags.All(holds))
            && (t.Terrains is null || t.Terrains.Count == 0 || (terrain is { } at && t.Terrains.Contains(at)))
            && (t.Done is not { } done || !isSet(done)))
        .OrderBy(t => t.Id.ToString(), StringComparer.Ordinal);

    /// <summary>One of the eligible talks by weight (a weight below 1 counts as 1), on the seed; null with none.</summary>
    public static CampTalkRecord? Choose(IEnumerable<CampTalkRecord> talks, IReadOnlyCollection<RecordId> party, Func<FlagGate, bool> holds,
        Func<RecordId, bool> isSet, int? terrain, uint seed)
    {
        var eligible = Eligible(talks, party, holds, isSet, terrain).ToList();
        if (eligible.Count == 0)
            return null;
        var total = eligible.Sum(t => (uint)Math.Max(t.Weight, 1));
        var pick = new Lcg(seed).Next() % total;
        foreach (var talk in eligible)
        {
            var weight = (uint)Math.Max(talk.Weight, 1);
            if (pick < weight)
                return talk;
            pick -= weight;
        }
        return eligible[^1];
    }
}
