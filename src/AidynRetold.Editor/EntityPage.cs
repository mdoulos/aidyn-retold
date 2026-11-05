using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// An entity's page (editor.md#an-entitys-page): an <see cref="EditPage"/> whose card is the record: its portrait and name, what kind
/// it is, its level, each attribute and skill as a button that asks for a new value (a skill it can never learn greyed, asking to
/// allow it), its spells, what it wears in slots that pick from what its skills and stats let it carry, and what it's worth in combat;
/// beside it its body's model with the model's textures; then, for one who teaches, the game's training window, the atlas where the
/// quest index has it, what names it and all its fields.
/// </summary>
public partial class EntityPage : EditPage
{
    private readonly EditorGame? _game;
    private readonly EntityRecord _entity;
    private readonly JsonObject _json;
    private readonly CharacterRules _rules;
    private readonly ItemCatalog? _items;
    private readonly RecordId? _scene;
    private readonly TrainingWindowPanel? _window;

    public EntityPage(PluginCatalog catalog, EditorGame? game, CharacterRules rules, ItemCatalog? items, EntityRecord entity, JsonObject json,
        IReadOnlyList<RecordReference> references, QuestIndex? quests, Action<RecordId> open, Func<Task> reload, AidynRetold.Views.Models? models,
        Func<Task<TextureUsage>> usage)
        : base(catalog, open, reload, models, usage)
    {
        (_game, _entity, _json, _rules, _items) = (game, entity, json, rules, items);
        _scene = entity.Body?.Actor is { } actor ? catalog.Plugins.TryGet<ActorRecord>(actor)?.Scene : null;
        var cards = new List<Control>();
        // Someone who teaches has the game's training window, on the editor's game, as a script's Train opens it.
        var teaches = Trainer.From(entity, rules, catalog.Plugins.Get<SpellRecord>);
        if (game is not null && items is not null && (teaches.SkillsOffered.Any() || teaches.Spells.Count > 0))
            _window = new TrainingWindowPanel(catalog.Plugins, game, teaches);
        // The atlas only where the quest index has something on it.
        if (quests is not null && QuestIndex.Covers(entity.Id))
        {
            var atlas = new AtlasView();
            atlas.Show(catalog, quests, entity.Id, open);
            if (atlas.Count > 0)
                cards.Add(new Section("Atlas", atlas, flush: true) { Note = $"{atlas.Count:N0} {(atlas.Count == 1 ? "entry" : "entries")}" });
            else
                atlas.QueueFree();
        }
        Build(json, references, cards);
    }

    /// <summary>The training window, if the entity teaches anything.</summary>
    public TrainingWindowPanel? Window => _window;

    /// <summary>As Trainer: the game's training window at it, as wide as the card, for one who teaches.</summary>
    protected override IEnumerable<(string Title, Control Page)> ExtraTabs => _window is null ? [] : [("As Trainer", _window)];

    protected override bool NameBesidePicture => true;

    // The attributes and skills stand in three columns, which need the room the labels would take.
    protected override float LabelWidth => 190;

    protected override RecordId Id => _entity.Id;

    protected override string RecordName => _entity.Name;

    protected override RecordId? Icon => _entity.Portrait;

    protected override string PictureFieldName => "portrait";

    protected override string PictureHelp => "Its portrait: beside what it says, and on the Party tab.";

    protected override string PictureDialogText => "Pick one of the portraits the game's characters have, or a PNG of your own, which saving puts in your mod.";

    protected override IEnumerable<RecordId> PictureSources() =>
        Catalog.Plugins.Ids.Where(i => i.Type is RecordTypes.Entity or RecordTypes.DialogEntity)
            .Select(i => (string?)Catalog.Plugins.GetJson(i)?["portrait"]).OfType<string>()
            .Select(t => RecordId.TryParse(t, out var r) ? r : (RecordId?)null).OfType<RecordId>();

    protected override RecordId? RecordModel => _scene;

    /// <summary>What kind of entity it is, as the record has it: which rows and slots the page shows.</summary>
    private EntityKind Kind => _entity.Kind;

    protected override float PictureSize => 80;

    /// <summary>Its body's outfits, where its actor has more than one, for the model card's arrows.</summary>
    protected override IReadOnlyList<ActorOutfit>? Outfits =>
        _entity.Body?.Actor is { } actor && Catalog.Plugins.TryGet<ActorRecord>(actor)?.Outfits is { Count: > 1 } outfits ? outfits : null;

    /// <summary>The outfit it wears as it starts: the first its gear meets, as <c>ActorOutfits.Meets</c> asks.</summary>
    protected override int FirstOutfit(IReadOnlyList<ActorOutfit> outfits)
    {
        var gear = new[] { _entity.Armor, _entity.Shield }.Concat(_entity.Weapons).OfType<RecordId>().ToList();
        var armored = _entity.Armor is { } armor && Catalog.Plugins.TryGet<ArmorRecord>(armor) is { Protection: > 0 };
        for (var i = 0; i < outfits.Count; i++)
        {
            var o = outfits[i];
            if ((o.Armored is not { } a || a == armored) && (o.Items is not { Count: > 0 } items || items.Any(gear.Contains)))
                return i;
        }
        return 0;
    }

    protected override string CardTitle => _entity.Kind switch
    {
        EntityKind.Party => "Party Member",
        EntityKind.Enemy => "Enemy",
        EntityKind.Character => "Character",
        _ => "Entity",
    };

    protected override string RecordsFile => "entities.json";

    protected override string Noun => "portrait";

    protected override bool ShowsModel => true;

    protected override bool ShowsModelRow => false;

    protected override string ModelPlace => "for its body";

    protected override string NoModelText => "No body: the game draws nothing for it, as for a speaker that never stands in the world.";

    // ---- The rows ----------------------------------------------------------------------------------------------------------------

    protected override void AddValueRows()
    {
        var e = _entity;
        AddRow("As it starts", new Label { Text = SheetLine(), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill },
            "The sheet the game builds from the record: its hit points are its Endurance, Stamina and Level, and its experience its level's.");
        AddRow("Kind", Choice("category", e.Category, Enum.GetValues<EntityCategory>()),
            "The entity DB's group. Party is the party; Chaos and Natural are enemies, and so is a Humanoid worth experience; Named and Generic are the characters. Charming fails on Chaos.");
        var school = EditorStyle.Row(16);
        var choice = Choice("school", e.School, Enum.GetValues<MagicSchool>(), s => s switch
        {
            MagicSchool.None => "Unschooled",
            MagicSchool.Chaos => "Chaos (every school)",
            _ => s.ToString(),
        });
        choice.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        school.AddChild(choice);
        var spells = SpellList();
        spells.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        school.AddChild(spells);
        AddRow("School and spells", school, "Which spells it can learn (unschooled, only unschooled spells; Chaos, every school), and the spells it knows, at their levels.");
        // The True Name is the story's: a script gives it, and the page never offers it. Heavy matters to the enemies' and characters' foes alone.
        if (Kind != EntityKind.Party)
            AddRow("Heavy", Switch("heavy", e.Heavy, "Heavy"), "Spells that need a heavy target work on it.");
        AddRow("Level", Number("level", e.Level, Math.Min(1, e.Level), Math.Max(_rules.Progression.Levels.Count - 1, e.Level)), "Its level as it starts, which its experience and hit points follow.");
        var columns = EditorStyle.Row(16);
        columns.AddChild(Column("Attributes", StatButtons()));
        columns.AddChild(Column("Skills", SkillButtons(SkillId.All.Where(s => s.IsSkill))));
        columns.AddChild(Column("Weapon skills", SkillButtons(SkillId.All.Where(s => s.IsWeapon || s.IsShield))));
        AddRow("Attributes and skills", columns,
            "Its base attributes and skill levels. Click one to change it, up to its base cap; a greyed skill is one it can never learn, which a click can allow. The weapon skills and the shield skill say what it can wield and bear.");
        AddRow("Equipment", Slots(), SlotsHelp());
        // What a party member never uses is left out: its morale, and what beating it is worth; a character's loot and perception too.
        if (Kind != EntityKind.Party)
        {
            AddRow("Morale", Number("morale", e.Morale, 0, 255), "How likely an enemy is to flee: 12 or more never flees.");
            AddRow("Experience", Number("experience", e.Experience, 0, 65535), "What beating it is worth, before the award multiplier.");
        }
        if (Kind == EntityKind.Enemy)
        {
            AddRow("Loot table", Number("lootCategory", e.LootCategory, 0, 255), "Which loot table a beaten enemy rolls.");
            AddRow("Perception", Number("perception", e.Perception, 0, 255), "How far it notices the party in trek: 10, or 0 for one that never does.");
        }
        AddRow("Protection", Number("protection", e.Protection, 0, 255), "Its base protection, which armor and spells add to.");
        AddRow("Damage", Number("damage", e.Damage, 0, 255), "Its base damage, which gear adds to.");
        if (e.Resistances.Count > 0)
        {
            AddRow("Resistances", new Label { Text = string.Join(", ", e.Resistances.Select(r => $"{r.Damage.ToString(CultureInfo.InvariantCulture)} × {r.Element}")) },
                "What each element's damage is multiplied by: 0 is immune, 2 vulnerable. All fields below lists them.");
        }
    }

    /// <summary>The sheet as the game builds it from the record with the changes held, or the record's alone where they don't read.</summary>
    private Character Sheet()
    {
        var record = _entity;
        if (Changes.Count > 0)
        {
            try
            {
                var merged = (JsonObject)_json.DeepClone();
                var patch = new JsonObject();
                foreach (var (field, value) in Changes)
                    patch[field] = value?.DeepClone();
                MergePatch.Apply(merged, patch, typeof(EntityRecord), _ => { });
                record = merged.Deserialize<EntityRecord>(PluginJson.Options) ?? _entity;
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException or FormatException)
            {
                record = _entity;
            }
        }
        try
        {
            return Character.Create(record, _rules, Catalog.Plugins.Get<SpellRecord>, flag => _game?.Network.IsSet(flag) ?? false, _items);
        }
        catch (Exception e) when (e is KeyNotFoundException or ArgumentException)
        {
            return Character.Create(_entity, _rules, Catalog.Plugins.Get<SpellRecord>, flag => _game?.Network.IsSet(flag) ?? false, _items);
        }
    }

    private string SheetLine()
    {
        var c = Sheet();
        var next = c.NextLevelExp is { } n ? $", level {c.Level + 1} at {n:N0}" : ", the last level";
        return $"Level {c.Level} · {c.Hp} of {c.MaxHp} hit points · {c.TotalExp:N0} experience{next}.";
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private string NameOf(RecordId id) => (string?)Catalog.Plugins.GetJson(id)?["name"] ?? id.Key;

    // ---- Attributes --------------------------------------------------------------------------------------------------------------

    private static readonly Stat[] Attributes = [Stat.Intelligence, Stat.Willpower, Stat.Dexterity, Stat.Endurance, Stat.Strength, Stat.Stamina];

    /// <summary>An attribute's base value as it reads now: the change held for the attributes, else the record's.</summary>
    private int StatValue(Stat stat)
    {
        var key = Camel(stat.ToString());
        if (TryChanged("attributes", out var held) && held is JsonObject o && o[key] is JsonValue v && v.TryGetValue(out int n))
            return n;
        var a = _entity.Attributes;
        return stat switch
        {
            Stat.Intelligence => a.Intelligence, Stat.Willpower => a.Willpower, Stat.Dexterity => a.Dexterity,
            Stat.Endurance => a.Endurance, Stat.Strength => a.Strength, _ => a.Stamina,
        };
    }

    /// <summary>A column of the attributes and skills row: its heading over its buttons, one under another.</summary>
    private static Control Column(string heading, IEnumerable<Button> buttons)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        column.AddChild(new Label { Text = heading, ThemeTypeVariation = "SubheadLabel" });
        foreach (var button in buttons)
        {
            // Narrow enough that three columns fit beside the model, the text cut where they can't.
            (button.Alignment, button.SizeFlagsHorizontal, button.CustomMinimumSize) = (HorizontalAlignment.Left, SizeFlags.ExpandFill, new Vector2(110, 0));
            (button.ClipText, button.TextOverrunBehavior) = (true, TextServer.OverrunBehavior.TrimEllipsis);
            column.AddChild(button);
        }
        return column;
    }

    private IEnumerable<Button> StatButtons()
    {
        foreach (var stat in Attributes)
        {
            var record = _rules[stat];
            var value = StatValue(stat);
            var button = new Button { Text = $"{record.Name}: {value}", TooltipText = $"{record.Name}, 1 to {record.BaseCap}. Click to change it." };
            // An enemy's may stand past the party's cap, and stays there until it's changed.
            button.Pressed += () => AskNumber(record.Name, value, Math.Min(1, value), Math.Max(record.BaseCap, value), n => SetStat(stat, n));
            yield return button;
        }
    }

    private void SetStat(Stat stat, int value)
    {
        var attributes = TryChanged("attributes", out var held) && held is JsonObject o ? (JsonObject)o.DeepClone() : (JsonObject?)_json["attributes"]?.DeepClone() ?? [];
        attributes[Camel(stat.ToString())] = value;
        Change("attributes", attributes, _json["attributes"]?.DeepClone());
        BuildRows();
    }

    // ---- Skills ------------------------------------------------------------------------------------------------------------------

    /// <summary>The field and key a skill is in the record: the shield skill its own field, the rest in the two objects.</summary>
    private static (string Field, string? Key) Place(SkillId skill) =>
        skill.IsShield ? ("shieldSkill", null) : skill.IsSkill ? ("skills", Camel(skill.Skill.ToString())) : ("weaponSkills", Camel(skill.Weapon.ToString()));

    /// <summary>A skill's base level as it reads now, or null for one it can never learn.</summary>
    private int? SkillLevel(SkillId skill)
    {
        var (field, key) = Place(skill);
        if (TryChanged(field, out var held))
        {
            if (key is null)
                return held is JsonValue v && v.TryGetValue(out int n) ? n : null;
            if (held is JsonObject o && o.ContainsKey(key))
                return o[key] is JsonValue kv && kv.TryGetValue(out int kn) ? kn : null;
        }
        return Character.SkillLevels(_entity)[skill.Slot];
    }

    private IEnumerable<Button> SkillButtons(IEnumerable<SkillId> skills)
    {
        foreach (var skill in skills)
        {
            var record = _rules[skill];
            var level = SkillLevel(skill);
            var button = new Button
            {
                Text = $"{record.Name}: {(level is { } l ? l.ToString(CultureInfo.InvariantCulture) : "—")}",
                TooltipText = level is null ? $"{record.Name}: it can never learn it. Click to allow it." : $"{record.Name}, 0 to {record.BaseCap}. Click to change it.",
            };
            if (level is null)
                button.Modulate = new Color(1, 1, 1, 0.45f);
            button.Pressed += () =>
            {
                if (level is { } current)
                {
                    AskNumber(record.Name, current, 0, Math.Max(record.BaseCap, current), n => SetSkill(skill, n), new DialogButton("Can Never Learn", DialogButtonKind.Secondary, () => SetSkill(skill, null)));
                }
                else
                {
                    new EditorDialog($"Allow {record.Name}?", EditorDialog.Paragraphs($"{_entity.Name} can't learn {record.Name}. Would you like {_entity.Name} to be able to learn it?"),
                    [
                        new DialogButton("Cancel", DialogButtonKind.Cancel),
                        new DialogButton("Allow", DialogButtonKind.Primary, () => SetSkill(skill, 0)),
                    ]).Open(this);
                }
            };
            yield return button;
        }
    }

    /// <summary>
    /// Holds a skill's level, or null for one it can never learn: in the record's own shape, so an object put back as it was reads as
    /// no change, and a skill made unlearnable is an explicit null, which the merge removes.
    /// </summary>
    private void SetSkill(SkillId skill, int? level)
    {
        var (field, key) = Place(skill);
        var original = _json[field]?.DeepClone();
        if (key is null)
        {
            Change(field, level, original);
        }
        else
        {
            var held = TryChanged(field, out var now) && now is JsonObject o ? (JsonObject)o.DeepClone() : (JsonObject?)original?.DeepClone() ?? [];
            if (level is { } n)
                held[key] = n;
            else if (held.ContainsKey(key) || (_json[field] as JsonObject)?.ContainsKey(key) == true)
                held[key] = null;
            Change(field, held, original);
        }
        BuildRows();
    }

    /// <summary>A dialog asking for a whole number, from <paramref name="min"/> to <paramref name="max"/>: its title, the box and the buttons, with any other choices after Cancel.</summary>
    private void AskNumber(string what, int value, int min, int max, Action<int> use, params DialogButton[] others)
    {
        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Rounded = true, Value = value, UpdateOnTextChanged = true, SelectAllOnFocus = true, CustomMinimumSize = new Vector2(150, 0) };
        var dialog = new EditorDialog($"Change {what}", [spin],
            [new DialogButton("Cancel", DialogButtonKind.Cancel), .. others, new DialogButton("Use", DialogButtonKind.Primary, () => use((int)spin.Value))]);
        dialog.Open(this);
        spin.GetLineEdit().CallDeferred(Control.MethodName.GrabFocus);
    }

    // ---- Spells ------------------------------------------------------------------------------------------------------------------

    private Control SpellList()
    {
        if (_entity.Spells.Count == 0)
            return EditorStyle.DimLabel(Character.SkillLevels(_entity)[SkillId.FromRecord(RecordId.Decimal(RecordTypes.Skill, (int)Skill.Wizard)).Slot] is null
                ? "None, and it can't learn Wizard, so it can learn no spells." : "None.");
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 6);
        flow.AddThemeConstantOverride("v_separation", 6);
        foreach (var known in _entity.Spells)
        {
            var button = new Button { Text = $"{NameOf(known.Spell)}  {known.Level}", TooltipText = $"{known.Spell}: open it" };
            button.Pressed += () => Open(known.Spell);
            flow.AddChild(button);
        }
        return flow;
    }

    // ---- Equipment ---------------------------------------------------------------------------------------------------------------

    /// <summary>A slot: what it holds as it reads now, how to hold another, and which items it may hold for the sheet as it is.</summary>
    private sealed record Slot(string Label, Func<RecordId?> Get, Action<RecordId?> Set, Func<Character, Item, bool> Allows, string Hidden);

    /// <summary>The gear kinds a party member has a slot for, one each.</summary>
    private static readonly ItemKind[] GearKinds = [ItemKind.Helmet, ItemKind.Cloak, ItemKind.Gloves, ItemKind.Ring, ItemKind.Wand, ItemKind.Belt, ItemKind.Boots, ItemKind.Amulet, ItemKind.Scroll];

    private string SlotsHelp() => _entity.Kind == EntityKind.Party
        ? "What it wears as it joins, and the weapons it brings into the inventory. Each slot picks from what its skills and stats let it carry."
        : "What it fights with: its weapon, the one it draws first in battle, its armor and its shield. Each slot picks from what its skills let it wield.";

    private JsonArray WeaponsNow() => TryChanged("weapons", out var held) && held is JsonArray a ? a : (JsonArray?)_json["weapons"] ?? [];

    private RecordId? WeaponAt(int slot) => WeaponsNow() is var w && slot < w.Count && Text(w[slot]) is { } text && RecordId.TryParse(text, out var id) ? id : null;

    private void SetWeapon(int slot, RecordId? item)
    {
        var weapons = (JsonArray)WeaponsNow().DeepClone();
        while (weapons.Count < 3)
            weapons.Add(null);
        weapons[slot] = item?.ToString();
        Change("weapons", weapons, _json["weapons"]?.DeepClone());
        BuildRows();
    }

    private RecordId? FieldItem(string field) => Text(Shown(field, _json[field])) is { } text && RecordId.TryParse(text, out var id) ? id : null;

    private void SetFieldItem(string field, RecordId? item)
    {
        Change(field, item?.ToString(), _json[field]?.DeepClone());
        BuildRows();
    }

    private List<RecordId> GearNow() => (TryChanged("gear", out var held) ? held as JsonArray : _json["gear"] as JsonArray)?
        .Select(Text).OfType<string>().Select(t => RecordId.TryParse(t, out var id) ? id : (RecordId?)null).OfType<RecordId>().ToList() ?? [];

    private RecordId? GearOf(ItemKind kind) => GearNow().FirstOrDefault(g => _items?.TryGet(g)?.Kind == kind) is { } id && id != default ? id : null;

    private void SetGear(ItemKind kind, RecordId? item)
    {
        var gear = GearNow().Where(g => _items?.TryGet(g)?.Kind != kind).ToList();
        if (item is { } added)
            gear.Add(added);
        Change("gear", gear.Count == 0 ? null : new JsonArray([.. gear.Select(g => (JsonNode?)g.ToString())]), _json["gear"]?.DeepClone());
        BuildRows();
    }

    private IEnumerable<Slot> SlotsFor()
    {
        static bool Wields(Character c, Item i) => i.Kind == ItemKind.Weapon && c.Equipment.CanWield(i) == EquipRefusal.None;
        static bool Wears(Character c, Item i) => i.Kind == ItemKind.Armor;
        static bool Bears(Character c, Item i) => i.Kind == ItemKind.Shield && c.Equipment.CanCarryShield;
        var lacks = "it lacks the skill or the Strength";
        if (_entity.Kind == EntityKind.Party)
        {
            yield return new("Weapon", () => WeaponAt(0), i => SetWeapon(0, i), Wields, lacks);
            yield return new("Armor", () => FieldItem("armor"), i => SetFieldItem("armor", i), Wears, "");
            yield return new("Shield", () => FieldItem("shield"), i => SetFieldItem("shield", i), Bears, "it lacks the shield skill");
            foreach (var kind in GearKinds)
            {
                var k = kind;
                yield return new(k.ToString(), () => GearOf(k), i => SetGear(k, i), (c, i) => i.Kind == k && c.Equipment.CanWear(i) == EquipRefusal.None, "it lacks the Strength or Intelligence");
            }
            yield return new("Brings", () => WeaponAt(1), i => SetWeapon(1, i), (_, i) => i.Kind == ItemKind.Weapon, "");
            yield return new("Brings too", () => WeaponAt(2), i => SetWeapon(2, i), (_, i) => i.Kind == ItemKind.Weapon, "");
        }
        else
        {
            yield return new("Weapon", () => WeaponAt(0), i => SetWeapon(0, i), Wields, lacks);
            yield return new("Battle weapon", () => WeaponAt(2), i => SetWeapon(2, i), Wields, lacks);
            yield return new("Armor", () => FieldItem("armor"), i => SetFieldItem("armor", i), Wears, "");
            yield return new("Shield", () => FieldItem("shield"), i => SetFieldItem("shield", i), Bears, "it lacks the shield skill");
        }
    }

    /// <summary>The slots, two across: each a button naming what it holds, with the item's picture, which picks another.</summary>
    private Control Slots()
    {
        if (_items is null)
            return EditorStyle.DimLabel("The plugins have no items to pick from.");
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        foreach (var slot in SlotsFor())
        {
            var held = slot.Get();
            var item = held is { } id ? _items.TryGet(id) : null;
            var button = new Button
            {
                Text = item is null ? $"{slot.Label}: none" : $"{slot.Label}: {item.Name}", Alignment = HorizontalAlignment.Left, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, TooltipText = held is { } h ? $"{h}: click to change it" : "Empty: click to fill it",
                ExpandIcon = true, CustomMinimumSize = new Vector2(0, 40), TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            };
            if (item is not null && Texture(item.Icon) is { } icon)
                button.Icon = icon;
            button.Pressed += () => PickFor(slot);
            grid.AddChild(button);
        }
        return grid;
    }

    /// <summary>What an item does, in a phrase: its numbers, then its bonuses.</summary>
    private string Does(Item item)
    {
        var parts = new List<string>();
        if (item.Weapon is { } w)
            parts.Add($"damage {w.Damage}, hit {w.Hit}, range {w.Range}" + (w.DamageType != Element.None ? $", {w.DamageType}" : ""));
        if (item.Armor is { } a)
            parts.Add($"protection {a.Protection}" + (a.Dexterity != 0 ? $", Dexterity {a.Dexterity:+0;-0}" : "") + (a.Stealth != 0 ? $", Stealth {a.Stealth:+0;-0}" : ""));
        if (item.Shield is { } s)
            parts.Add($"defense {s.Defense}, protection {s.Protection}" + (s.Dexterity != 0 ? $", Dexterity {s.Dexterity:+0;-0}" : ""));
        if (item.Gear is { } g && (g.Protection != 0 || g.Damage != 0))
            parts.Add(string.Join(", ", new[] { g.Protection != 0 ? $"protection {g.Protection}" : "", g.Damage != 0 ? $"damage {g.Damage}" : "" }.Where(t => t.Length > 0)));
        parts.AddRange(ItemText.Does(NameOf, item));
        return parts.Count == 0 ? "—" : string.Join("; ", parts);
    }

    /// <summary>
    /// The dialog that fills a slot: the items the slot may hold for the sheet as it is, each with what it does, and none; the rest are
    /// hidden, and counted. A double click or <b>Use</b> holds the one picked until <b>Save</b>.
    /// </summary>
    private void PickFor(Slot slot)
    {
        if (_items is null)
            return;
        var sheet = Sheet();
        var list = new Tree { ThemeTypeVariation = "FlushTree", Columns = 2, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row };
        list.SetColumnCustomMinimumWidth(0, 200);
        list.SetColumnExpand(0, false);
        list.SetColumnClipContent(1, true);
        var root = list.CreateItem();
        var none = list.CreateItem(root);
        none.SetText(0, "None");
        none.SetCustomColor(0, EditorStyle.Dim);
        none.SetMetadata(0, "");
        var current = slot.Get();
        TreeItem? selected = current is null ? none : null;
        var hidden = 0;
        foreach (var item in _items.All.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (item.Kind is ItemKind.Potion or ItemKind.Key or ItemKind.Misc)
                continue;
            if (!slot.Allows(sheet, item))
            {
                hidden += slot.Allows(sheet, item) || KindFits(slot, item) ? 1 : 0;
                continue;
            }
            var row = list.CreateItem(root);
            row.SetText(0, item.Name);
            if (Texture(item.Icon) is { } icon)
            {
                row.SetIcon(0, icon);
                row.SetIconMaxWidth(0, 20);
            }
            row.SetTooltipText(0, item.Id.ToString());
            row.SetText(1, Does(item));
            row.SetTooltipText(1, Does(item));
            row.SetMetadata(0, item.Id.ToString());
            if (item.Id == current)
                selected = row;
        }
        var box = new PanelContainer { ThemeTypeVariation = "ListBox", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(new FittedTable(list, 12, inBox: true));
        RecordId? Picked() => list.GetSelected()?.GetMetadata(0).AsString() is { Length: > 0 } text && RecordId.TryParse(text, out var id) ? id : null;
        EditorDialog? dialog = null;
        void Use()
        {
            slot.Set(Picked());
            dialog?.Close();
        }
        list.ItemActivated += Use;
        list.Ready += () =>
        {
            if (selected is not null)
            {
                selected.Select(0);
                list.ScrollToItem(selected);
            }
        };
        var words = $"What {_entity.Name} may hold as its {slot.Label.ToLowerInvariant()}, by its skills and stats as the record has them, each with what it does."
            + (hidden > 0 && slot.Hidden.Length > 0 ? $" {hidden} {(hidden == 1 ? "is" : "are")} hidden: {slot.Hidden}." : "");
        dialog = new EditorDialog($"Change the {slot.Label.ToLowerInvariant()}", [.. EditorDialog.Paragraphs(words, PictureGrid), box],
            [
                new DialogButton("Cancel", DialogButtonKind.Cancel),
                new DialogButton("Use", DialogButtonKind.Primary, Use, Closes: false),
            ], width: PictureGrid + 2 * EditorDialog.Inset);
        dialog.Open(this);
    }

    /// <summary>Whether an item is of a slot's kind at all, for the count of those hidden for what the sheet lacks.</summary>
    private static bool KindFits(Slot slot, Item item) => slot.Label switch
    {
        "Weapon" or "Battle weapon" or "Brings" or "Brings too" => item.Kind == ItemKind.Weapon,
        "Armor" => item.Kind == ItemKind.Armor,
        "Shield" => item.Kind == ItemKind.Shield,
        _ => item.Kind.ToString() == slot.Label,
    };
}
