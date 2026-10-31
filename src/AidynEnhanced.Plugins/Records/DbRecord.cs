namespace AidynEnhanced.Plugins.Records;

/// <summary>
/// A DB record's name and portrait, keyed by its ItemID (<c>aidyn:entity/0x023F</c>). Dialogue
/// entities have only these; entities, spells, weapons, armor, items and loot tables have records of
/// their own, which read as this too.
/// </summary>
/// <param name="Portrait">
/// The image beside what an entity or a dialogue entity says (<c>EntityDB::GetPortrait</c>). None
/// for other records.
/// </param>
/// <param name="Body">How an entity or a dialogue entity stands in the world. None for other records.</param>
/// <param name="Voice">
/// Whether the speaker is a man or a woman, for the pitch of the port's text voice (<c>AidynEnhanced.Core.Ui.TextReveal</c>): the
/// port's own, which the importer judges by the portrait (<c>AidynEnhanced.Rom.Import.Voices</c>) and a mod can patch. None for a
/// creature, which is pitched by its height alone.
/// </param>
/// <param name="VoicePitch">
/// The pitch of the blips the port's conversation box sounds as this speaker's lines print, as a multiple of the blip's own: the port's
/// own, which a mod sets outright in place of the voice and height; none, as the importer writes.
/// </param>
public sealed record DbRecord(RecordId Id, string Name, RecordId? Portrait = null, EntityBody? Body = null, Voice? Voice = null, float? VoicePitch = null);

/// <summary>A speaker's voice, for the port's text voice: a man's or a woman's. A creature has none.</summary>
public enum Voice
{
    Male,
    Female,
}

/// <summary>
/// How an entity or a dialogue entity stands in the world, as a wanderer or a cut-scene's actor
/// (<c>gEntityExtras</c>; <c>EntityDB::GetBorg7</c>, <c>GetCollideRadius</c>,
/// <c>GetScale</c> and <c>GetHeight</c>).
/// </summary>
/// <param name="Actor">Its model. A record the tables leave out is a bat, as the code makes it.</param>
/// <param name="Radius">Its collision sphere's radius, before <paramref name="Scale"/>. Every dialogue entity's is 0.55, which the code gives them.</param>
/// <param name="Scale">How much bigger than its actor's model it's drawn, and how much bigger its sphere is when bodies push each other.</param>
/// <param name="Height">About how tall its model is at a scale of 1, in scene units.</param>
/// <param name="Strike">
/// The share of its attack's animation at which the blow lands and the target starts to react (<c>EntityBody.StrikePoint</c>,
/// <c>0x8006C7FC</c>, which <c>Bodies.Process</c> reads in combat); 0.4 for a dialogue entity or one the tables leave out.
/// </param>
public sealed record EntityBody(RecordId Actor, float Radius, float Scale, float Height, float Strike = EntityBody.DefaultStrike)
{
    /// <summary>The strike's share for a record the tables leave out (<c>0x800D2CF8</c>).</summary>
    public const float DefaultStrike = 0.4f;
}
