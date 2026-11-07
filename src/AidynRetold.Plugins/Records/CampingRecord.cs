namespace AidynRetold.Plugins.Records;

/// <summary>
/// How camping goes (<c>aidyn:camping/0</c>, the only one): the ambush, the heal and the camp scenes. Each terrain's
/// chance of an ambush, and whether camping is allowed there at all, is its <see cref="TerrainRecord.Camp"/>.
/// </summary>
/// <param name="CannotCamp">What's said where the terrain allows no camping.</param>
/// <param name="Ambushes">While this flag is set, camping can be ambushed.</param>
/// <param name="AmbushBase">
/// The ambush's chance in percent is trunc((<paramref name="AmbushBase"/> − the party's best modified Ranger, at most
/// <paramref name="RangerCap"/>) × <paramref name="AmbushScale"/> × the terrain's factor), in single precision.
/// </param>
/// <param name="Heal">The share of maximum hit points and of every battery's capacity that camping restores.</param>
/// <param name="EffectTicks">Ticks of play that pass for the members' potions and other effects as they heal.</param>
/// <param name="Checking">Set while the game looks for a camp scene, which the scenes' flags read.</param>
/// <param name="Scenes">
/// A dialogue object in the player's chunk whose enabling flag is one of these, and holds, starts as the party camps,
/// the first found, wherever it is.
/// </param>
/// <param name="Campsite">
/// With no camp scene, this map's first dialogue object starts: the original's Alaron alone at the fire ("pass the marshmallows").
/// None plays nothing in its place, as the remade game has it (the user's choice, Nov 7, 2026).
/// </param>
/// <param name="AmbushLevels">
/// The highest average party level of each of the ambush's tiers but the last (<see cref="TerrainRecord.Ambushes"/>): 5, 10,
/// 15 and 20 (<c>CampEncounters.Load</c>). Null in plugins imported before them.
/// </param>
/// <param name="RestHours">
/// The hours a night's rest is, the original's 8 (<c>World::Lapse8Hours</c>): what a story camp scene's <c>rest</c> passes, and the
/// rest the camping record's effect ticks are for, which a longer or shorter rest scales. The remade game's camp rests until morning,
/// noon or evening or for an hour, as the player chooses (the user's design, Nov 7, 2026), whatever is made there.
/// </param>
/// <param name="Recharge">
/// The share of every battery's capacity that camping restores, or none for <paramref name="Heal"/>'s, as the original refills them
/// by the same share it heals. The remade game heals nothing at camp and keeps the batteries' half (the user's choice, Nov 7, 2026).
/// </param>
public sealed record CampingRecord(
    RecordId Id,
    string CannotCamp,
    RecordId Ambushes,
    int AmbushBase,
    int RangerCap,
    float AmbushScale,
    float Heal,
    int EffectTicks,
    RecordId Checking,
    IReadOnlyList<RecordId> Scenes,
    RecordId? Campsite = null,
    IReadOnlyList<int>? AmbushLevels = null,
    int RestHours = 8,
    float? Recharge = null);
