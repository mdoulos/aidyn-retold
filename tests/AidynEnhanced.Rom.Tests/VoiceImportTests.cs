using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Rom.Tests;

/// <summary>The voices the importer gives speakers by their portraits (<see cref="Import.Voices"/>).</summary>
[Collection(ImportedPluginCollection.Name)]
public sealed class VoiceImportTests(Na10Import fixture)
{
    [Fact]
    public void EveryEntityNamedForItsSexHasThatVoice()
    {
        var entities = fixture.Data.Set.All<EntityRecord>(RecordTypes.Entity).ToList();
        Assert.NotEmpty(entities);
        foreach (var e in entities.Where(e => e.Name.Contains("Male") || e.Name.Contains("Female")))
            Assert.Equal(e.Name.Contains("Female") ? Voice.Female : Voice.Male, e.Voice);
    }

    /// <summary>The portraits of the Chaos entities that are men all the same: the Chaos knights, the Marquis, Shamsuk and Sheridan.</summary>
    private static readonly HashSet<RecordId> Faced =
        [.. new[] { 0x0175, 0x0157, 0x0134, 0x016A }.Select(n => RecordId.Hex(RecordTypes.Image, n))];

    [Fact]
    public void EveryPersonHasAVoiceAndNoCreatureDoes()
    {
        var entities = fixture.Data.Set.All<EntityRecord>(RecordTypes.Entity).ToList();
        // The named people, the townsfolk and the party speak; of the rest only the Dryads, the Gorgon and the Harpy have a face.
        var voiced = entities.Where(e => e.Voice is not null).Select(e => e.Name).ToHashSet();
        foreach (var e in entities.Where(e => e.Category is EntityCategory.Named or EntityCategory.Generic or EntityCategory.Party))
            Assert.True(e.Voice is not null || e.Name is "Rooughah" or "Zaratas" or "Lugash", $"{e.Name} ({e.Id}) has no voice.");
        foreach (var e in entities.Where(e => e.Category is EntityCategory.Natural or EntityCategory.Chaos))
            Assert.True(e.Voice is null or Voice.Deep || Faced.Contains(e.Portrait!.Value), $"{e.Name} ({e.Id}) has a voice.");
        Assert.Contains("Alaron", voiced);
        Assert.Contains("Brenna", voiced);
    }

    [Fact]
    public void TheGuardsAndTheGiantsAreDeep()
    {
        var entities = fixture.Data.Set.All<EntityRecord>(RecordTypes.Entity).ToList();
        foreach (var e in entities.Where(e => e.Name is "Town Guard" or "Jundar Guard" or "Ogre" or "Cyclops" or "Troll" or "Bandit Brute"))
            Assert.Equal(Voice.Deep, e.Voice);
        Assert.Equal(Voice.Male, entities.Single(e => e.Name == "Alaron").Voice);
    }

    [Fact]
    public void DialogueEntitiesSpeakByTheirPortraitsToo()
    {
        var speakers = fixture.Data.Set.All<DbRecord>(RecordTypes.DialogEntity).ToDictionary(d => d.Name);
        Assert.Equal(Voice.Male, speakers["Amann"].Voice);
        Assert.Equal(Voice.Female, speakers["Lycea"].Voice);
        Assert.Null(speakers["Cow"].Voice);
        Assert.Null(speakers["Kingdom Sign"].Voice);
    }
}
