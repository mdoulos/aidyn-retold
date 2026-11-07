namespace AidynRetold.Plugins.Records;

/// <summary>
/// A named place the party can be in, the remade game's (the user's design, Nov 7, 2026): what the game calls where the player is,
/// "Gwernia" the moment he steps into its streets, "Oisin Forest" on the road between it and Erromon. A place names some chunks of a
/// map set, some world map regions (<see cref="ChunkEntry.Region"/>), or whole map sets, any or all; the player is in the most
/// particular place that names his chunk (<see cref="At"/>). The original has no such thing: its world map shows a ring and its towns'
/// names, and nothing says where the party stands.
/// </summary>
/// <param name="Name">What the game calls it.</param>
/// <param name="Chunks">Chunks of map sets, each by name, "M07", as the chunk's (<see cref="ChunkEntry.Name"/>).</param>
/// <param name="Regions">World map regions, by their flags: every chunk carrying one.</param>
/// <param name="MapSets">Whole map sets: every chunk of each, a town's interiors.</param>
public sealed record PlaceRecord(
    RecordId Id,
    string Name,
    [property: KeyedList(nameof(PlaceChunks.MapSet))] IReadOnlyList<PlaceChunks>? Chunks = null,
    IReadOnlyList<RecordId>? Regions = null,
    IReadOnlyList<RecordId>? MapSets = null)
{
    /// <summary>Whether the place names this chunk of the map set itself.</summary>
    public bool NamesChunk(RecordId mapSet, ChunkEntry chunk) => Chunks?.Any(c => c.MapSet == mapSet && c.Names(chunk)) == true;

    /// <summary>Whether the place names the chunk's world map region.</summary>
    public bool NamesRegion(ChunkEntry chunk) => chunk.Region is { } region && Regions?.Contains(region) == true;

    /// <summary>Whether the place names the whole map set.</summary>
    public bool NamesMapSet(RecordId mapSet) => MapSets?.Contains(mapSet) == true;

    /// <summary>
    /// The place the player is in, at a chunk of a map set: the first place, in ID order, naming the chunk itself; else the first naming
    /// its region; else the first naming the map set; else null, where nothing names it yet. So a town's chunks name the town inside a
    /// region that names the forest round it.
    /// </summary>
    public static PlaceRecord? At(PluginSet plugins, RecordId mapSet, ChunkEntry chunk)
    {
        var places = plugins.All<PlaceRecord>(RecordTypes.Place).OrderBy(p => p.Id.ToString(), StringComparer.Ordinal).ToList();
        return places.FirstOrDefault(p => p.NamesChunk(mapSet, chunk))
            ?? places.FirstOrDefault(p => p.NamesRegion(chunk))
            ?? places.FirstOrDefault(p => p.NamesMapSet(mapSet));
    }
}

/// <summary>Some chunks of one map set, by name.</summary>
/// <param name="Chunks">The chunks' names, "M07" (<see cref="ChunkEntry.Name"/>), in any case.</param>
public sealed record PlaceChunks(RecordId MapSet, IReadOnlyList<string> Chunks)
{
    public bool Names(ChunkEntry chunk) => Chunks.Any(n => string.Equals(n, chunk.Name, StringComparison.OrdinalIgnoreCase));
}
