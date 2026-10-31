using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Rom.Import;

/// <summary>
/// Which speakers are men and which women, by their portraits, for the pitch of the port's text voice (the port's own content,
/// docs/hardcoded-content.md; <c>AidynEnhanced.Core.Ui.TextReveal</c>). The original has no such field: the hurt cries in the audio keys
/// split the party by sex, but the townsfolk, who do most of the talking, have no keys at all. Every entity and dialogue entity with a
/// human face in its portrait is here, judged by eye from the portraits (Oct 30, 2026), the shared commoner and shopkeeper faces among
/// them; creatures, elementals, the Chaos things, the signs and the animals have no voice and are pitched by their height alone. A mod
/// patches a record's <c>voice</c> or sets its <c>voicePitch</c> outright. The guards, the brutes and the giants are deep (<see cref="Voice.Deep"/>),
/// as deep as each other whatever their height, the user's choice.
/// </summary>
public static class Voices
{
    private static readonly Dictionary<int, Voice> ByPortrait = new()
    {
        // Men.
        [0x0106] = Voice.Male, // Abrecan
        [0x0108] = Voice.Male, // Alaron
        [0x0109] = Voice.Male, // Amann
        [0x010B] = Voice.Male, // Arturo
        [0x010C] = Voice.Male, // Baird
        [0x010D] = Voice.Male, // Baldric
        [0x010E] = Voice.Male, // Balfin
        [0x010F] = Voice.Male, // the hooded bandits
        [0x0111] = Voice.Male, // Batrin
        [0x0113] = Voice.Male, // Becan
        [0x0115] = Voice.Male, // Bowden
        [0x0118] = Voice.Male, // Cadme
        [0x0119] = Voice.Male, // Cedric
        [0x0125] = Voice.Male, // Male Commoner, and the shopkeepers who share his face
        [0x0126] = Voice.Male, // Male Commoner
        [0x0127] = Voice.Male, // Male Commoner
        [0x0128] = Voice.Male, // Cradawgh
        [0x012A] = Voice.Male, // Damek
        [0x012B] = Voice.Male, // Darmath
        [0x012C] = Voice.Male, // Devlin
        [0x012D] = Voice.Male, // Donovan
        [0x012E] = Voice.Male, // Dougal
        [0x0130] = Voice.Male, // Male Dryad
        [0x0131] = Voice.Male, // Phelan
        [0x0133] = Voice.Male, // Edur
        [0x0134] = Voice.Male, // Shamsuk
        [0x0135] = Voice.Male, // Farris
        [0x0137] = Voice.Male, // Fyrsil
        [0x0138] = Voice.Male, // Gabrion
        [0x013B] = Voice.Male, // Gavinn
        [0x013D] = Voice.Male, // Gillek
        [0x0140] = Voice.Male, // Godric
        [0x0149] = Voice.Male, // Herne
        [0x014E] = Voice.Male, // Male Jundar, Honza and Ugarit's shopkeepers
        [0x0150] = Voice.Male, // Kolokos
        [0x0155] = Voice.Male, // Mago
        [0x0157] = Voice.Male, // Marquis
        [0x015B] = Voice.Male, // Erromon's men, Mirari Male
        [0x015D] = Voice.Male, // Niesen
        [0x016A] = Voice.Male, // Sheridan
        [0x0170] = Voice.Male, // Tamberlain
        [0x0171] = Voice.Male, // Trahern
        [0x0173] = Voice.Male, // Txomin
        [0x0175] = Voice.Male, // the Chaos knights: Assim, Behrooz, Golnar, Ksathra, Mehrdad, Nasim, Shatrevar
        [0x017B] = Voice.Male, // Yuli
        [0x017C] = Voice.Male, // Zaratas
        // Big men, deep whatever their height, the user's choice: the guards, and the brutes and giants that speak.
        [0x0144] = Voice.Deep, // Town Guard
        [0x0145] = Voice.Deep, // Town Guard
        [0x0146] = Voice.Deep, // Town Guard
        [0x014D] = Voice.Deep, // Jundar Guard
        [0x014B] = Voice.Deep, // the bandit brutes: Bandit Brute, Goon and Thug
        [0x011D] = Voice.Deep, // Chaos Mauler and Slayer
        [0x0129] = Voice.Deep, // Cyclops
        [0x014A] = Voice.Deep, // Hobgoblin, Hobgoblin Major and Kitarak
        [0x0159] = Voice.Deep, // Minotaur Lord
        [0x3846] = Voice.Deep, // Minotaur
        [0x015E] = Voice.Deep, // Ogre and Ogre Guard
        [0x015F] = Voice.Deep, // Ogre Boss
        [0x0172] = Voice.Deep, // Troll
        // Women.
        [0x010A] = Voice.Female, // Ardra
        [0x0116] = Voice.Female, // Brenna
        [0x0121] = Voice.Female, // Charisa
        [0x0122] = Voice.Female, // Female Commoner, and the shopkeepers who share her face
        [0x0123] = Voice.Female, // Female Commoner
        [0x0124] = Voice.Female, // Female Commoner
        [0x012F] = Voice.Female, // Female Dryad
        [0x013C] = Voice.Female, // Gechina
        [0x0141] = Voice.Female, // Gorgon
        [0x0142] = Voice.Female, // Gotzone
        [0x0147] = Voice.Female, // Harpy
        [0x014C] = Voice.Female, // Female Jundar and Ugarit's shopkeepers
        [0x014F] = Voice.Female, // Keelin
        [0x0154] = Voice.Female, // Lycea
        [0x0158] = Voice.Female, // Meriona
        [0x015A] = Voice.Female, // Erromon's women
        [0x015C] = Voice.Female, // Niala
        [0x0160] = Voice.Female, // Oriana
        [0x0161] = Voice.Female, // Pandara
        [0x0164] = Voice.Female, // Rheda
        [0x016B] = Voice.Female, // Sholeh
        [0x016F] = Voice.Female, // Swagma
        [0x0179] = Voice.Female, // Xibid
        [0x017A] = Voice.Female, // Yeraza
        [0x017E] = Voice.Female, // Zurene
    };

    private static readonly Dictionary<RecordId, Voice> ById = ByPortrait.ToDictionary(p => RecordId.Hex(RecordTypes.Image, p.Key), p => p.Value);

    /// <summary>
    /// The speakers whose pitch is set outright rather than by their voice and height, by portrait, the user's choice by ear
    /// (Oct 30, 2026): Abrecan 0.69 halfway between Alaron's 0.72 and Baird's 0.667 by height, then a tenth lower; Baird 0.57;
    /// Becan 0.60, 15% under his 0.70 by height.
    /// </summary>
    private static readonly Dictionary<RecordId, float> PitchById = new()
    {
        [RecordId.Hex(RecordTypes.Image, 0x0106)] = 0.62f, // Abrecan
        [RecordId.Hex(RecordTypes.Image, 0x010C)] = 0.57f, // Baird
        [RecordId.Hex(RecordTypes.Image, 0x0113)] = 0.60f, // Becan
    };

    /// <summary>
    /// The speakers whose blips have a tone of their own (<c>TextReveal.Tones</c>), by portrait, the user's choice (Oct 30, 2026): the
    /// guards and Abrecan clipped and precise, Brenna gentle, and the rest of the party given the other tones to compare.
    /// </summary>
    private static readonly Dictionary<RecordId, string> ToneById = new Dictionary<int, string>
    {
        [0x0144] = "soldier", // Town Guard
        [0x0145] = "soldier", // Town Guard
        [0x0146] = "soldier", // Town Guard
        [0x014D] = "soldier", // Jundar Guard
        [0x0106] = "soldier", // Abrecan
        [0x0116] = "soft", // Brenna
        [0x010B] = "bright", // Arturo
        [0x0135] = "bright", // Farris
        [0x0164] = "bright", // Rheda
        [0x010C] = "buzzy", // Baird
        [0x012D] = "hollow", // Donovan
        [0x014F] = "hollow", // Keelin
        [0x016B] = "hollow", // Sholeh
        [0x0113] = "gravel", // Becan
        [0x0140] = "gravel", // Godric
        [0x015D] = "gravel", // Niesen
    }.ToDictionary(p => RecordId.Hex(RecordTypes.Image, p.Key), p => p.Value);

    /// <summary>The tone of a speaker with this portrait, or none for the plain one.</summary>
    public static string? ToneOf(RecordId? portrait) => portrait is { } id && ToneById.TryGetValue(id, out var tone) ? tone : null;

    /// <summary>The voice of a speaker with this portrait, or none for a creature's or no portrait.</summary>
    public static Voice? Of(RecordId? portrait) => portrait is { } id && ById.TryGetValue(id, out var voice) ? voice : null;

    /// <summary>The pitch set outright for a speaker with this portrait, or none to pitch it by its voice and height.</summary>
    public static float? PitchOf(RecordId? portrait) => portrait is { } id && PitchById.TryGetValue(id, out var pitch) ? pitch : null;
}
