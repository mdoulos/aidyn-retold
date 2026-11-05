using System.Globalization;
using AidynRetold.Core;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The characters the editor works with: an entity's sheet as its record starts it, or a copy of
/// the editor's game's party to try things on.
/// </summary>
public static class EditorCharacters
{
    /// <summary>The character rules, or null if the plugins are older than the characters' records.</summary>
    public static CharacterRules? Rules(PluginSet plugins)
    {
        try
        {
            return CharacterRules.From(plugins);
        }
        catch (Exception e) when (e is KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The items, or null if the plugins are older than the item records.</summary>
    public static ItemCatalog? Items(PluginSet plugins)
    {
        try
        {
            return ItemCatalog.From(plugins);
        }
        catch (Exception e) when (e is KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>An entity as its record starts it, with its True Name by the editor's flags, wearing its gear.</summary>
    public static Character? Create(PluginSet plugins, CharacterRules rules, RecordId entity, EditorGame? game, ItemCatalog? items) =>
        plugins.TryGet<EntityRecord>(entity) is { } record
            ? Character.Create(record, rules, plugins.Get<SpellRecord>,
                flag => game is not null && game.Network.IsSet(flag), items)
            : null;

    /// <summary>
    /// A copy of the editor's game's party to try things on, with its gold and inventory, or for plugins
    /// too old for a game, a new game's Alaron alone.
    /// </summary>
    public static Party CopyParty(PluginSet plugins, CharacterRules rules, EditorGame? game, ItemCatalog items)
    {
        if (game is not null)
            return game.CopyParty();
        var party = new Party(items);
        if (Create(plugins, rules, GameState.Alaron, null, items) is { } alaron)
            party.Add(alaron);
        return party;
    }

    /// <summary>A member of the editor's party as pages that try things on it name them: "New game Alaron", or a loaded game's member by name.</summary>
    public static string Label(EditorGame? game, Character member) => game is { IsNewGame: false } ? member.Name : $"New game {member.Name}";
}

/// <summary>
/// An entity's page: its character sheet as its record starts it, built by the core's rules (<c>Entity::Init</c>): its hit points,
/// experience, attributes, skills, spells and what it wears, and what it's worth in combat. A trainer's page ends with the game's
/// training window. The page shows the record; it tries nothing out.
/// </summary>
public partial class CharacterPanel : PagePanel
{
    private readonly EditorGame? _game;
    private readonly EntityRecord _entity;
    private readonly CharacterRules _rules;
    private readonly ItemCatalog? _items;
    private readonly Character _character;
    private readonly Party? _party;
    private TrainingWindowPanel? _window;

    public CharacterPanel(PluginCatalog catalog, EditorGame? game, EntityRecord entity, CharacterRules rules, ItemCatalog? items, Action<RecordId> open)
        : base(catalog, open)
    {
        (_game, _entity, _rules, _items) = (game, entity, rules, items);
        // The sheet as the record starts it, alone in a party, so the weapons it brings and doesn't wield go where they would.
        _character = EditorCharacters.Create(Plugins, _rules, _entity.Id, _game, _items)!;
        _party = _items is null ? null : new Party(_items);
        _party?.Add(_character);
        // Someone who teaches has the training window, on the editor's game, as a script's Train opens it.
        var teaches = Trainer.From(entity, rules, catalog.Plugins.Get<SpellRecord>);
        if (game is not null && items is not null && (teaches.SkillsOffered.Any() || teaches.Spells.Count > 0))
            _window = new TrainingWindowPanel(catalog.Plugins, game, teaches);
        ShowSheet();
    }

    /// <summary>The training window, if the entity teaches anything.</summary>
    public TrainingWindowPanel? Window => _window;

    private void ShowSheet()
    {
        ClearBody();
        var c = _character;
        Body.AddChild(Header());

        Heading("Attributes");
        var stats = Grid(["", "Base", "Modified", "Base cap"]);
        foreach (var stat in Enum.GetValues<Stat>())
            Row(stats, [_rules[stat].Name, Num(c.Stats.Base(stat)), Modified(c.Stats.Modified(stat), c.Stats.Base(stat)), Num(_rules[stat].BaseCap)]);
        Body.AddChild(stats);

        Heading("Skills");
        var skills = Grid(["", "Level", "Modified"]);
        foreach (var skill in SkillId.All.Where(c.Skills.CanLearn))
            Row(skills, [_rules[skill].Name, Num(c.Skills.Base(skill)), Modified(c.Skills.Modified(skill), c.Skills.Base(skill))]);
        Body.AddChild(skills);
        var never = SkillId.All.Where(s => !c.Skills.CanLearn(s)).Select(s => _rules[s].Name).ToList();
        if (never.Count > 0)
            Line($"Can never learn: {string.Join(", ", never)}.", Dim);

        Heading("Spells");
        if (c.Spells.Count == 0)
            Line(c.Skills.CanLearn(Skill.Wizard) ? "None." : "None, and it can't learn Wizard, so it can learn no spells.", Dim);
        else
        {
            var grid = Grid(["", "Level", "School", "Wizard"]);
            foreach (var known in c.Spells)
            {
                grid.AddChild(LinkButton(known.Spell.Id, known.Spell.Name));
                Row(grid, [Num(known.Level), known.Spell.School.ToString(), Num(known.Spell.Wizard)]);
            }
            Body.AddChild(grid);
        }

        if (_party is not null)
            ShowEquipment();

        Heading("In combat");
        var armor = c.Equipment.ArmorProtection;
        Line($"Protection {c.Protection + armor}{(armor != 0 ? $" ({armor} of it from armor and shield)" : "")}, damage {c.Damage}, morale {_entity.Morale}. "
            + $"Worth {_entity.Experience:N0} experience before the award's × {_rules.Progression.ExpAward.ToString(CultureInfo.InvariantCulture)}.");
        if (_entity.Resistances.Count > 0)
            Line("Takes " + string.Join(", ", _entity.Resistances.Select(r => $"{r.Damage.ToString(CultureInfo.InvariantCulture)} × {r.Element} damage")) + ".");
        if (_party is null)
        {
            foreach (var gear in _entity.Weapons.Append(_entity.Armor).Append(_entity.Shield).OfType<RecordId>())
                Body.AddChild(LinkButton(gear, Catalog.Find(gear)?.Summary ?? gear.ToString()));
        }

        if (_window is not null)
        {
            Heading("As a trainer");
            Body.AddChild(_window);
        }
    }

    /// <summary>What it wears as it starts, by the core's rules, each with what it does.</summary>
    private void ShowEquipment()
    {
        var equipment = _character.Equipment;
        Heading("Equipment");
        var worn = Grid(["", "", "What it does"]);
        var slots = new List<(string Slot, ItemInstance? Item)> { ("Weapon", equipment.Weapon), ("Armor", equipment.Armor), ("Shield", equipment.Shield) };
        slots.AddRange(equipment.Gear.Select((g, i) => ($"Gear {i + 1}", g)));
        foreach (var (slot, item) in slots)
        {
            if (item is null)
                continue;
            worn.AddChild(new Label { Text = slot, ThemeTypeVariation = "DimLabel" });
            worn.AddChild(ItemLink(item.Id, item.Item.Icon, item.Item.Name));
            var does = ItemText.Does(NameOf, item.Item).Append(ItemText.State(item)).Where(t => t.Length > 0).ToList();
            worn.AddChild(new Label { Text = does.Count == 0 ? "—" : string.Join("; ", does) });
        }
        if (worn.GetChildCount() == 3)
            Line("Nothing.", Dim);
        else
            Body.AddChild(worn);
        if (equipment.Enchantments.Any())
            Line("On while worn: " + string.Join(", ", equipment.Enchantments.Select(e => $"{NameOf(e.Spell)} at level {e.Level}")) + ".");
        // The weapons it brings and doesn't wield, which go to the party's inventory as it joins.
        var brought = _party!.Inventory.Stacks;
        if (brought.Count > 0)
            Line("Brings: " + string.Join(", ", brought.Select(s => s.Quantity > 1 ? $"{s.Item.Name} × {s.Quantity}" : s.Item.Name)) + ".", Dim);
    }

    private Control Header()
    {
        var c = _character;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        if (_entity.Portrait is { } portrait && Picture(portrait, 2) is { } picture)
            row.AddChild(picture);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var name = new Label { Text = c.Name };
        name.AddThemeFontSizeOverride("font_size", 20);
        text.AddChild(name);
        var next = c.NextLevelExp is { } n ? $", level {c.Level + 1} at {n:N0}" : ", the last level";
        text.AddChild(new Label { Text = $"Level {c.Level} · {c.Hp} of {c.MaxHp} hit points · {c.TotalExp:N0} experience{next}" });
        var traits = new List<string> { _entity.Category.ToString(), c.Aspect.ToString(), c.School == MagicSchool.None ? "unschooled" : $"{c.School} school" };
        if (c.TrueName)
            traits.Add("True Name");
        else if (_entity.TrueNameFlag is { } flag)
            traits.Add($"True Name from {flag}");
        if (c.Heavy)
            traits.Add("heavy");
        text.AddChild(new Label { Text = string.Join(" · ", traits), ThemeTypeVariation = "DimLabel" });
        row.AddChild(text);
        return row;
    }

    private static string Modified(int modified, int @base) => modified == @base ? Num(modified) : $"{modified} ({modified - @base:+0;-0})";
}
