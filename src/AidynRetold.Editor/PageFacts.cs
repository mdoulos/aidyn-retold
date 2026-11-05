using AidynRetold.Plugins;

namespace AidynRetold.Editor;

/// <summary>
/// What a <see cref="RecordPage"/> knows of a record kind beyond its class: what one is called, which field is its picture, which
/// fields it leaves out, and for each field a label, a line saying what it is, and its bounds; and which record type a field that
/// names records names, where the record's own value can't say (an empty list, none). A kind the facts don't name gets a page of
/// its fields' names in words, with no help.
/// </summary>
public static class PageFacts
{
    /// <summary>A field's label, help and bounds; <see cref="Default"/> is what a new row of a list starts at.</summary>
    public sealed record FieldFact(string? Label = null, string Help = "", int? Min = null, int? Max = null, int? Default = null);

    /// <summary>A kind: its names, its picture field, the fields its page leaves out, and its fields' facts.</summary>
    public sealed record Kind(string Singular, string Plural, string? Picture = null, string PictureHelp = "", IReadOnlySet<string>? Hidden = null,
        IReadOnlyDictionary<string, FieldFact>? Fields = null)
    {
        public IReadOnlySet<string> Hidden { get; init; } = Hidden ?? new HashSet<string>();

        public FieldFact Field(string field) => Fields?.GetValueOrDefault(field) ?? new FieldFact();
    }

    private static readonly FieldFact None = new();

    private static readonly Dictionary<string, Kind> Kinds = new()
    {
        [RecordTypes.Spell] = new("Spell", "spells", "icon", "Its symbol, on the Spells list and the spell's details.", new HashSet<string> { "unk1E", "position", "aspect" }, new Dictionary<string, FieldFact>
        {
            ["school"] = new(Help: "Which school's casters learn it; unschooled, anyone's."),
            ["damage"] = new(Help: "Its damage dice, d6 each, with the margin's; 0 for a spell that doesn't hurt.", Max: 255),
            ["stamina"] = new(Help: "What casting it costs, less the casting level, at least 1.", Max: 255),
            ["targetCount"] = new(Help: "One target, several, all of a side, or the caster alone."),
            ["targetType"] = new(Help: "Whom it may be cast at, or the field."),
            ["wizard"] = new(Help: "The Wizard rank it needs, to learn and to cast.", Max: 15),
            ["special"] = new(Help: "What its target must be: nothing, a True Name, a heavy one."),
            ["range"] = new(Help: "How far it reaches, in cells, times the casting level for a placed one.", Max: 255),
            ["reagent"] = new(Help: "The reagent a cast uses up, from the party's inventory."),
            ["trainingCost"] = new(Help: "What a trainer asks for it, a level, in gold.", Max: 65535),
            ["description"] = new(Help: "What the spell's details say of it."),
            ["cast"] = new(Help: "The sound as it's cast."),
            ["effects"] = new(Help: "What it does as it lands, a record each: All fields below lists them."),
            ["duration"] = new(Help: "How long its lasting effects last: ticks a level, a flat count, or the battle."),
            ["placed"] = new(Help: "Cast at a cell the player picks, not a target."),
            ["resisted"] = new(Help: "Who rolls to resist it."),
            ["notResistedBy"] = new(Help: "Entities that never resist it."),
            ["aiValue"] = new(Help: "What the enemies' AI makes of it; none takes its first effect's kind's."),
            ["visuals"] = new(Help: "What it shows as it's cast and lands: All fields below lists them."),
            ["area"] = new(Help: "Whether it lands on everyone near its target."),
        }),
        [RecordTypes.Skill] = new("Skill", "skills", "icon", "Its icon on the Skills list.", null, new Dictionary<string, FieldFact>
        {
            ["description"] = new(Help: "What the skill screen says of it."),
            ["trainingCost"] = new(Help: "What a trainer asks a level, in gold.", Max: 65535),
            ["baseCap"] = new(Help: "The most a base level can be trained to.", Max: 15),
            ["cap"] = new(Help: "The most a modified level can reach, with gear's bonuses.", Max: 20),
        }),
        [RecordTypes.WeaponSkill] = new("Weapon skill", "weapon skills", "icon", "Its icon on the Skills list.", null, new Dictionary<string, FieldFact>
        {
            ["description"] = new(Help: "What the skill screen says of it; the monsters' weapon skills have none."),
            ["trainingCost"] = new(Help: "What a trainer asks a level, in gold; the monsters' cost nothing.", Max: 65535),
            ["baseCap"] = new(Help: "The most a base level can be trained to.", Max: 15),
            ["cap"] = new(Help: "The most a modified level can reach, with gear's bonuses.", Max: 20),
        }),
        [RecordTypes.Stat] = new("Attribute", "attributes", null, "", null, new Dictionary<string, FieldFact>
        {
            ["description"] = new(Help: "What the stat's details say of it."),
            ["baseCap"] = new(Help: "The most its base value can be raised to.", Max: 255),
            ["cap"] = new(Help: "The most its modified value can reach, with gear's bonuses.", Max: 255),
            ["trainingCost"] = new(Help: "The experience to raise it by one, times the value it's at; none for the Level, which experience raises.", Max: 65535),
        }),
        [RecordTypes.Progression] = new("Levels and training", "progressions", null, "", null, new Dictionary<string, FieldFact>
        {
            ["levels"] = new(Help: "The experience each level starts at, level 0 first."),
            ["expAward"] = new(Help: "What every award of experience is multiplied by."),
            ["training"] = new(Help: "What training costs and allows."),
            ["training.firstLevelGold"] = new(Help: "What a skill's first level costs in gold.", Max: 65535),
            ["training.goldPerLevel"] = new(Help: "What each level after it adds to the price.", Max: 65535),
            ["training.trainerExp"] = new(Help: "A trainer's experience price is the full price times this."),
            ["training.spellMaxLevel"] = new(Help: "The most a spell can be trained to.", Max: 15),
            ["training.freeTrainers"] = new(Help: "The entities that train for nothing."),
        }),
        [RecordTypes.Loot] = new("Loot table", "loot tables", null, "", null, new Dictionary<string, FieldFact>
        {
            ["goldMin"] = new("Gold, least", "The gold it gives is rolled between the bounds and doubled.", Max: 65535),
            ["goldMax"] = new("Gold, most", "", Max: 65535),
            ["armorChance"] = new(Help: "The percent chance a beaten enemy drops its armor.", Max: 100),
            ["shieldChance"] = new(Help: "The percent chance a beaten enemy drops its shield.", Max: 100),
            ["weaponChances"] = new(Help: "The percent chance a beaten enemy drops each of its three weapons.", Max: 100),
            ["reagentChance"] = new(Help: "The percent chance of a reagent, picked first.", Max: 100),
            ["reagentMin"] = new("Reagents, least", "", Max: 255),
            ["reagentMax"] = new("Reagents, most", "", Max: 255),
            ["drops"] = new(Help: "The items it may give, each its own roll: only the first two give a count between their bounds."),
            ["drops.chance"] = new(Max: 100),
            ["drops.min"] = new(Max: 255, Default: 1),
            ["drops.max"] = new(Max: 255, Default: 1),
        }),
        [RecordTypes.Shop] = new("Shop", "shops", null, "", null, new Dictionary<string, FieldFact>
        {
            ["shopkeeper"] = new(Help: "Who runs it: a script's shop command opens the shop of whoever is speaking."),
            ["stock"] = new(Help: "What it sells, each at its price times a multiplier."),
        }),
        [RecordTypes.Recipe] = new("Recipe", "recipes", null, "", new HashSet<string> { "unk5" }, new Dictionary<string, FieldFact>
        {
            ["skill"] = new(Help: "The skill that makes it: Alchemist brews, Mechanic smiths, Cook cooks."),
            ["product"] = new(Help: "What it makes, one each time."),
            ["ingredients"] = new(Help: "What it uses up, whether the check passes or not."),
            ["ingredients.count"] = new(Max: 99, Default: 1),
            ["level"] = new(Help: "The skill level it's offered from.", Max: 15),
            ["step"] = new(Help: "The check's step it needs; 0 for any that passes.", Max: 255),
            ["failure"] = new(Help: "What a failed check makes instead, if anything."),
        }),
        [RecordTypes.DialogEntity] = new("Speaker", "speakers", "portrait", "Its portrait beside what it says.", null, new Dictionary<string, FieldFact>
        {
            ["body"] = new(Help: "How it stands in the world, as a wanderer."),
            ["voice"] = new(Help: "The port's text voice: a man's or a woman's."),
        }),
        [RecordTypes.Journal] = new("Journal entry", "journal entries", null, "", null, new Dictionary<string, FieldFact>
        {
            ["flag"] = new(Help: "The flag the journal lists it under while it's set."),
            ["text"] = new(Help: "What the journal says."),
        }),
        [RecordTypes.Trigger] = new("Trigger", "triggers", null, "", null, new Dictionary<string, FieldFact>
        {
            ["on"] = new(Help: "The event it fires on."),
            ["subject"] = new(Help: "The record the event must be about, if any."),
            ["do"] = new(Help: "What it does, in order: All fields below lists the ops."),
        }),
        [RecordTypes.Terrain] = new("Terrain", "terrains", null, "", null, new Dictionary<string, FieldFact>
        {
            ["sky"] = new(Help: "The sky image drawn over chunks of this terrain."),
        }),
        [RecordTypes.Magic] = new("Magic rules", "magic rules", null, "", null, new Dictionary<string, FieldFact>
        {
            ["roundTicks"] = new(Help: "How long a battle's round is, in ticks.", Max: 65535),
            ["mostDisabled"] = new(Help: "How many enemies the party's holds and charms may disable at once.", Max: 12),
            ["improvement"] = new(Help: "How much a cast raises a spell's level toward the next."),
            ["maxLevel"] = new(Help: "The most a spell's level can be.", Max: 15),
            ["spellEffects"] = new(Help: "How many spells' effects a character holds at once.", Max: 32),
            ["potionEffects"] = new(Help: "How many potions' effects a character holds at once.", Max: 32),
        }),
        [RecordTypes.Camping] = new("Camping", "camping rules", null, "", null, new Dictionary<string, FieldFact>
        {
            ["heal"] = new(Help: "The share of hit points and Stamina a camp gives back."),
            ["effectTicks"] = new(Help: "How many ticks a camp ages the party's effects by.", Max: 1000000),
            ["ambushBase"] = new(Help: "The base chance of an ambush, in percent.", Max: 100),
            ["rangerCap"] = new(Help: "The most Ranger counts for against an ambush.", Max: 15),
        }),
        [RecordTypes.Inventory] = new("Inventory rules", "inventory rules", null, "", null, new Dictionary<string, FieldFact>
        {
            ["stackLimit"] = new(Help: "The most of one item a stack holds.", Max: 255),
            ["generalSlots"] = new(Help: "How many stacks the general slots hold.", Max: 255),
        }),
    };

    /// <summary>The types the fields that name records name, where a value can't say: an empty list, or none.</summary>
    private static readonly Dictionary<(string Kind, string Field), string> Names = new()
    {
        [(RecordTypes.Shop, "shopkeeper")] = RecordTypes.Entity,
        [(RecordTypes.Shop, "stock.item")] = RecordTypes.Item,
        [(RecordTypes.Loot, "drops.item")] = RecordTypes.Item,
        [(RecordTypes.Recipe, "skill")] = RecordTypes.Skill,
        [(RecordTypes.Recipe, "product")] = RecordTypes.Item,
        [(RecordTypes.Recipe, "failure")] = RecordTypes.Item,
        [(RecordTypes.Recipe, "ingredients.item")] = RecordTypes.Item,
        [(RecordTypes.Spell, "notResistedBy")] = RecordTypes.Entity,
        [(RecordTypes.Spell, "cast")] = RecordTypes.Sequence,
        [(RecordTypes.Progression, "training.freeTrainers")] = RecordTypes.Entity,
        [(RecordTypes.Journal, "flag")] = RecordTypes.Flag,
        [(RecordTypes.Terrain, "sky")] = RecordTypes.Image,
        [(RecordTypes.DialogEntity, "portrait")] = RecordTypes.Image,
    };

    /// <summary>The kind's facts, or a plain one named by its type.</summary>
    public static Kind For(string type) => Kinds.GetValueOrDefault(type) ?? new Kind(
        RecordTypes.TryInfo(type)?.Singleton == true ? TypeGroups.Name(type) : TypeGroups.One(type) ?? Singular(TypeGroups.Name(type)),
        TypeGroups.Name(type).ToLowerInvariant());

    private static string Singular(string plural) => plural.EndsWith("ies", StringComparison.Ordinal) ? plural[..^3] + "y"
        : plural.EndsWith('s') && !plural.EndsWith("ss", StringComparison.Ordinal) ? plural[..^1] : plural;

    /// <summary>The record type a field that names records names, if the facts say.</summary>
    public static string? TypeOf(string kind, string field) => Names.GetValueOrDefault((kind, field));

    /// <summary>The records file a mod's patch of a kind goes in, as the importer names them: <c>spells.json</c>, <c>armor.json</c>, a singleton's <c>combat.json</c>.</summary>
    public static string FileOf(string type) => type switch
    {
        RecordTypes.Armor or RecordTypes.Journal or RecordTypes.Loot or RecordTypes.Strings or RecordTypes.Dialogue => $"{type}.json",
        RecordTypes.DialogEntity => "dialogentities.json",
        _ when RecordTypes.TryInfo(type)?.Singleton == true => $"{type}.json",
        _ => $"{type}s.json",
    };
}
