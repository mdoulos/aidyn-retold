using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Rng;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>What the editor says of items.</summary>
public static class ItemText
{
    /// <summary>What wearing or wielding an item does, a phrase each.</summary>
    public static IEnumerable<string> Does(Func<RecordId, string> name, Item item)
    {
        var bonuses = item.Bonuses;
        if (bonuses.Aspect != Aspect.None)
            yield return $"only for the {bonuses.Aspect} aspect";
        if (item.Armor is { } armor)
        {
            if (armor.Protection != 0)
                yield return $"{armor.Protection:+0;-0} protection";
            if (armor.Dexterity != 0)
                yield return $"{armor.Dexterity:+0;-0} Dexterity";
            if (armor.Stealth != 0)
                yield return $"{armor.Stealth:+0;-0} Stealth";
        }
        if (item.Gear is { } gear)
        {
            if (gear.Protection != 0)
                yield return $"{gear.Protection:+0;-0} protection";
            if (gear.Damage != 0)
                yield return $"{gear.Damage:+0;-0} damage";
        }
        if (bonuses.Stat is { } stat)
            yield return item.BatteryCapacity is { } capacity ? $"a battery of {capacity} Stamina for spells" : $"{stat.Amount:+0;-0} {name(stat.Stat)}";
        if (bonuses.Skill is { } skill)
            yield return $"{skill.Amount:+0;-0} {name(skill.Skill)}";
        if (bonuses.Spell is { } spell)
            yield return $"casts {name(spell.Spell)} at level {spell.Level}, {spell.Charges} charge{(spell.Charges == 1 ? "" : "s")}";
        if (bonuses.Enchantment is { } enchantment)
            yield return $"{name(enchantment.Spell)} at level {enchantment.Level} " + (item.Kind == ItemKind.Weapon ? "as it strikes" : "while worn");
        if (bonuses.Resistance is { } resistance)
            yield return $"takes {resistance.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture)} × {resistance.Element} damage";
    }

    /// <summary>What's left in an item's battery and of its charges, where it has them and they've been used.</summary>
    public static string State(ItemInstance item)
    {
        var parts = new List<string>();
        if (item.Battery is { } battery && battery != item.Item.BatteryCapacity)
            parts.Add($"battery at {battery} of {item.Item.BatteryCapacity}");
        if (item.Charges is { } charges && charges != item.Item.Bonuses.Spell?.Charges)
            parts.Add($"{charges} charges left");
        return string.Join(", ", parts);
    }

    /// <summary>Why a character can't equip an item, said of the character as "they".</summary>
    public static string Refusal(EquipRefusal refusal, Item item) => refusal switch
    {
        EquipRefusal.CantUse => item.Kind switch
        {
            ItemKind.Weapon => $"they can't wield {item.Weapon!.Class} weapons",
            ItemKind.Shield => "they have no shield skill",
            _ => "it isn't worn",
        },
        EquipRefusal.WrongAspect => $"it's only for the {item.Bonuses.Aspect} aspect",
        EquipRefusal.NeedsStrength => $"their base Strength is below {item.RequiredStrength}",
        EquipRefusal.NeedsIntelligence => $"their base Intelligence is below {item.RequiredIntelligence}",
        EquipRefusal.NoRoom => "they've no room for it",
        EquipRefusal.Fixed => "what it would replace can't be taken off",
        _ => "",
    };

    public static string Trade(TradeResult result) => result switch
    {
        TradeResult.CantAfford => "The party can't afford it.",
        TradeResult.StackFull => "The party's stack of it is full.",
        TradeResult.InventoryFull => "The inventory has no slot for it.",
        TradeResult.Refused => "The shopkeeper won't buy it: it would sell for 1 gold or less.",
        TradeResult.NoneLeft => "There's none of it.",
        _ => "",
    };

    public static string Stack(RecordId id, int count, ItemCatalog items) =>
        (items.TryGet(id)?.Name ?? id.ToString()) + (count > 1 ? $" × {count}" : "");
}

/// <summary>
/// A weapon's, armor's or item's page: its icon, what it is, and what wearing it does, with what it
/// would do for each of the editor's party, by the core's rules.
/// </summary>
public partial class ItemPanel : PagePanel
{
    private readonly EditorGame? _game;
    private readonly CharacterRules? _rules;
    private readonly ItemCatalog _items;
    private readonly Item _item;

    public ItemPanel(PluginCatalog catalog, EditorGame? game, CharacterRules? rules, ItemCatalog items, Item item, Action<RecordId> open)
        : base(catalog, open)
    {
        (_game, _rules, _items, _item) = (game, rules, items, item);
        Build();
    }

    private void Build()
    {
        var item = _item;
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        if (Picture(item.Icon, 3) is { } icon)
            header.AddChild(icon);
        var text = new VBoxContainer();
        var name = new Label { Text = item.Name };
        name.AddThemeFontSizeOverride("font_size", 20);
        text.AddChild(name);
        var traits = new List<string> { Kind(item), $"{item.Price:N0} gold" };
        if (item.Traits.OwnSlot)
            traits.Add("a slot of its own");
        if (item.Traits.MustTake)
            traits.Add("loot can't be left with it");
        if (item.Weapon?.Fixed == true)
            traits.Add("can't be taken off");
        text.AddChild(new Label { Text = string.Join(" · ", traits), ThemeTypeVariation = "DimLabel" });
        header.AddChild(text);
        Body.AddChild(header);

        if (item.Weapon is { } weapon)
        {
            Heading("Wielding it");
            Body.AddChild(LinkButton(RecordId.Decimal(RecordTypes.WeaponSkill, (int)weapon.Class), $"{weapon.Class} skill, which must be above 0"));
            Line($"Damage {weapon.Damage}, hit {weapon.Hit}, range {weapon.Range}" + (weapon.DamageType != Element.None ? $", {weapon.DamageType} damage" : "") + ".");
        }
        if (item.Shield is { } shield)
            Line($"Defense {shield.Defense}. Carrying it needs the shield skill.");
        var needs = new List<string>();
        if (item.RequiredStrength > 0)
            needs.Add($"base Strength {item.RequiredStrength}");
        if (item.RequiredIntelligence > 0)
            needs.Add($"base Intelligence {item.RequiredIntelligence}");
        if (needs.Count > 0)
            Line($"Needs {string.Join(" and ", needs)}.");

        var does = ItemText.Does(NameOf, item).ToList();
        if (does.Count > 0)
        {
            Heading(item.Weapon is not null ? "What it does" : "Wearing it");
            foreach (var phrase in does)
                Line(char.ToUpperInvariant(phrase[0]) + phrase[1..] + ".");
            foreach (var spell in new[] { item.Bonuses.Spell?.Spell, item.Bonuses.Enchantment?.Spell }.OfType<RecordId>().Distinct())
                Body.AddChild(LinkButton(spell, NameOf(spell)));
            if (item.Bonuses.Skill is not null)
                Line("A skill bonus counts only while the wearer's base level in it is above 0.", Dim);
        }

        if (item.KeyItem is { } key)
        {
            Heading("Key item");
            Line("While the party has it, it sets:");
            Body.AddChild(LinkButton(key.Held, $"flag {key.Held.Key}, cleared when the last is taken"));
            if (key.Region is { } region)
            {
                Line("When first gained, it reveals a region of the world map:");
                Body.AddChild(LinkButton(region, $"flag {region.Key}"));
            }
        }

        if (_rules is not null && (item.Kind is ItemKind.Weapon or ItemKind.Armor or ItemKind.Shield || item.IsGear))
            ShowOnParty(_rules);
    }

    private static string Kind(Item item) => item.Kind switch
    {
        ItemKind.Weapon => $"{item.Weapon!.Class} weapon",
        ItemKind.Armor => "body armor",
        ItemKind.Misc => "item",
        _ => item.Kind.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// For each of the editor's party, what putting it on would do to their sheet, or why they can't:
    /// equipped by the core on a fresh copy of the party. A scroll is read, as in trek.
    /// </summary>
    private void ShowOnParty(CharacterRules rules)
    {
        var count = NewParty(rules).Members.Count();
        var scroll = _item.Kind == ItemKind.Scroll;
        Heading(scroll ? "Reading it" : "Equipping it");
        var grid = Grid(["", ""]);
        for (var i = 0; i < count; i++)
        {
            var party = NewParty(rules);
            var member = party.Members.ElementAt(i);
            var before = Sheet(member, rules);
            var wornBefore = member.Equipment.All.ToList();
            party.Inventory.Add(_item);
            string result;
            if (_item.Kind == ItemKind.Scroll)
            {
                result = party.ReadScroll(member, _item.Id, Plugins.Get<SpellRecord>) switch
                {
                    ScrollResult.Learned => $"learns {NameOf(_item.Bonuses.Spell!.Spell)}",
                    ScrollResult.NotAWizard => "can't; they can't learn Wizard",
                    ScrollResult.NoSpell => "can't; it holds no spell",
                    ScrollResult.AlreadyKnown => "knows its spell already",
                    ScrollResult.NeedsWizard => $"can't; their base Wizard is below {Plugins.Get<SpellRecord>(_item.Bonuses.Spell!.Spell).Wizard}",
                    ScrollResult.WrongSchool => "can't; the spell is of another school",
                    _ => "",
                };
            }
            else if (party.Equip(member, _item.Id) is var refusal && refusal != EquipRefusal.None)
            {
                result = $"can't; {ItemText.Refusal(refusal, _item)}";
            }
            else
            {
                var changes = before.Zip(Sheet(member, rules)).Where(p => p.First.Value != p.Second.Value)
                    .Select(p => $"{p.First.Name} {p.First.Value} → {p.Second.Value}").ToList();
                var replaced = wornBefore.Where(w => !member.Equipment.All.Contains(w)).Select(w => w.Item.Name).ToList();
                result = (replaced.Count > 0 ? $"replacing {string.Join(", ", replaced)}: " : "")
                    + (changes.Count > 0 ? string.Join(", ", changes) : "no change to the sheet");
            }
            grid.AddChild(new Label { Text = EditorCharacters.Label(_game, member) });
            grid.AddChild(new Label { Text = result, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        }
        Body.AddChild(grid);
    }

    private Party NewParty(CharacterRules rules) => EditorCharacters.CopyParty(Plugins, rules, _game, _items);

    /// <summary>The values putting an item on can change, by name.</summary>
    private static List<(string Name, int Value)> Sheet(Character c, CharacterRules rules)
    {
        var values = Enum.GetValues<Stat>().Select(s => (rules[s].Name, c.Stats.Modified(s))).ToList();
        values.Add(("hit points", c.MaxHp));
        values.AddRange(SkillId.All.Where(c.Skills.CanLearn).Select(s => (rules[s].Name, c.Skills.Modified(s))));
        values.Add(("protection", c.Protection + c.Equipment.ArmorProtection));
        values.Add(("damage", c.Damage));
        return values;
    }
}
