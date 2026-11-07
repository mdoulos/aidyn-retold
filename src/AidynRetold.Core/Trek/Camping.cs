using AidynRetold.Core.Characters;
using AidynRetold.Core.Rng;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Trek;

/// <summary>How a camp went.</summary>
public enum CampOutcome
{
    /// <summary>The terrain allows no camping.</summary>
    Refused,
    /// <summary>The party was ambushed: no heal and no time passing. Trek fades out into the battle (<see cref="TrekLoop.CueBattle"/>).</summary>
    Ambushed,
    /// <summary>The party made camp: the camp screen opens, and the heal, the night's scene and the hours come with its rest (<see cref="TrekLoop.CampRest"/>).</summary>
    Camped,
}

/// <summary>What the night at camp shows, once the party rests.</summary>
public enum CampNight
{
    /// <summary>Nothing: the hours pass and trek goes on.</summary>
    None,
    /// <summary>A camp talk, in the conversation box over the camp screen, which closes as the talk ends.</summary>
    Talk,
    /// <summary>A story camp scene, or the Campsite's own, cued as a cut-scene: the camp screen closes as trek fades out into it.</summary>
    Scene,
}

/// <summary>How the rest went (<see cref="TrekLoop.CampRest"/>).</summary>
/// <param name="HoursNow">The hours to pass at once: the camp's, less the eight a <c>rest</c> script passes as it ends, if the night's is one.</param>
public sealed record CampRestResult(CampNight Night, int HoursNow);

/// <summary>Camping's ambush (<c>0x800788cc</c> in 1.0). See <see cref="CampingRecord"/>.</summary>
public static class Camping
{
    /// <summary>
    /// The ambush's chance in percent on a terrain with this factor: trunc((15 − r) × 1/15 × factor) in single precision,
    /// where r is the party's best modified Ranger (<c>GetMostSkilledMember</c>), 0 if it's 0 or less, at most 10.
    /// </summary>
    public static int AmbushChance(CampingRecord rules, float factor, Party party)
    {
        var ranger = party.MostSkilled(Skill.Ranger)?.Skills.Modified(Skill.Ranger) ?? 0;
        var r = Math.Clamp(ranger, 0, rules.RangerCap);
        var chance = (rules.AmbushBase - r) * rules.AmbushScale * factor;
        return (ushort)chance;
    }

    /// <summary>Whether a camp is ambushed: only while the camping record's flag holds, then d100 against the chance.</summary>
    public static bool Ambushed(CampingRecord rules, float factor, Party party, Func<RecordId, bool> isSet, IRandom random) =>
        isSet(rules.Ambushes) && random.RollD(1, 100) <= AmbushChance(rules, factor, party);
}
