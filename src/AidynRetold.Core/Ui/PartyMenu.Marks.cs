using System.Globalization;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>How an item in the grid stands with the member shown, which its cell marks (<see cref="PartyMenu.Mark"/>).</summary>
public enum ItemMark
{
    None,

    /// <summary>The member can't put it on, or learn its spell.</summary>
    Unusable,
}

/// <summary>
/// What the item box says of an item in the port's look (<see cref="PartyMenu.Card"/>): its name, a line of what it is, and why the member
/// shown can't use it. Whether it's better than what's worn is the player's to judge, the user's choice (Nov 5, 2026).
/// </summary>
public sealed record ItemCard(string Name, string? About = null, string? Warning = null);

/// <summary>
/// Marks on the grid's items for the member shown, in the port's look, the user's choice (Nov 3 and 5, 2026): what they can't use, under a
/// grey box with its icon faded; and the item's card, which says why.
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>
    /// How an item stands with the member shown: <see cref="ItemMark.Unusable"/> if they can't put it on, or for a scroll they'd read,
    /// learn its spell; none for what's worn, potions and the rest. Nothing marks what would be better than what they wear, the user's
    /// choice (Nov 5, 2026): that's theirs to judge.
    /// </summary>
    public ItemMark Mark(InventoryRow row) => Shown is not { } member || row.Wearer is not null ? ItemMark.None
        : Refusal(member, row.Item) is not null ? ItemMark.Unusable
        : ItemMark.None;

    /// <summary>
    /// Why the member can't use an item, put shortly, or null if they can: for one put on, its aspect, Strength, Intelligence or their
    /// skill with it (<see cref="Party.CanEquip"/>); for a scroll they'd read, the Wizard skill, its rank, its school, or knowing it.
    /// </summary>
    private string? Refusal(Character member, Item item)
    {
        if (item.Kind == ItemKind.Scroll && Picking is null && Combat is null)
        {
            if (item.Bonuses.Spell is not { } carried || _spells(carried.Spell) is not { } spell)
                return null;
            if (member.Skills.Base(Skill.Wizard) < 0)
                return "Needs the Wizard skill";
            if (member.FindSpell(spell.Id) is not null)
                return "Already known";
            return member.CanLearn(spell) switch
            {
                SpellLearnability.WizardTooLow => $"Needs Wizard rank {Num(spell.Wizard)}",
                SpellLearnability.WrongSchool => $"Of the school of {spell.School}",
                _ => null,
            };
        }
        if (item.Kind is ItemKind.Potion or ItemKind.Food or ItemKind.Misc or ItemKind.Key)
            return null;
        return Party.CanEquip(member, item) switch
        {
            EquipRefusal.WrongAspect => $"Needs the {item.Bonuses.Aspect} aspect",
            EquipRefusal.NeedsStrength => $"Needs {Num(item.RequiredStrength)} Strength",
            EquipRefusal.NeedsIntelligence => $"Needs {Num(item.RequiredIntelligence)} Intelligence",
            EquipRefusal.None => null,
            _ => $"{member.Name} can't use this",
        };
    }

    /// <summary>
    /// The card over an item, in the port's look: its name; a line of what it is, a weapon's skill, damage and to-hit, armor's
    /// protection, a shield's defense, gear's protection, damage and stat bonus, a scroll's spell; why the member shown can't use it,
    /// in red. Just the name for what's worn.
    /// </summary>
    public ItemCard Card(InventoryRow row)
    {
        var item = row.Item;
        if (Shown is not { } member)
            return new ItemCard(item.Name);
        var about = new List<string>();
        switch (item.Kind)
        {
            case ItemKind.Weapon when item.Weapon is { } weapon:
                about.Add(member.Rules[SkillId.FromRecord(RecordId.Decimal(RecordTypes.WeaponSkill, (int)weapon.Class))].Name);
                about.Add($"Damage {Num(weapon.Damage)}");
                about.Add($"Hit {Num(weapon.Hit)}");
                break;
            case ItemKind.Armor when item.Armor is { } armor:
                about.Add($"Protection {Num(armor.Protection)}");
                break;
            case ItemKind.Shield when item.Shield is { } shield:
                about.Add($"Defense {Num(shield.Defense)}");
                about.Add($"Protection {Num(shield.Protection)}");
                break;
            case ItemKind.Scroll when item.Bonuses.Spell is { } carried && _spells(carried.Spell) is { } spell:
                about.Add(spell.Name);
                break;
            case ItemKind.Potion or ItemKind.Food:
                // Not "Potion" or "Flask", which the name says: what it does, shortly, the user's choice (Nov 5, 2026).
                if (EffectsSummary(member, item) is { } summary)
                    about.Add(summary);
                break;
            default:
                if (item.Protection != 0)
                    about.Add($"Protection {Num(item.Protection)}");
                if (item.Damage != 0)
                    about.Add($"Damage {Num(item.Damage)}");
                break;
        }
        if (item.Bonuses.Stat is { } bonus && bonus.Stat != Item.StaminaStat)
            about.Add($"{(bonus.Amount > 0 ? "+" : "")}{Num(bonus.Amount)} {member.Rules[StatId.FromRecord(bonus.Stat)].Name}");
        if (row.Stack?.Left is { } left)
            about.Add(Spoilage.Describe(left));
        // A line that only says the name again, a scroll's named as its spell, "Aura of Death", says nothing, the user's choice (Nov 6, 2026).
        var text = about.Count > 0 ? string.Join(", ", about) : null;
        if (text == item.Name)
            text = null;
        if (row.Wearer is not null)
            return new ItemCard(item.Name, text);
        return new ItemCard(item.Name, text, Refusal(member, item));
    }

    /// <summary>
    /// What a potion's or a food's effects do, shortly, the user's choice (Nov 5, 2026): "+3 Stealth", "Heals 15-25%", "+8 Stamina",
    /// "Restores Stamina", "+6 vs. Elemental", "Cures", a flask's "Damage"; null with none it can say.
    /// </summary>
    private string? EffectsSummary(Character member, Item item)
    {
        if (item.Gear?.Effects is not { Count: > 0 } effects)
            return null;
        var parts = new List<string>();
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case StatEffect stat:
                    parts.Add($"{Signed(stat.Amount.At(0))} {member.Rules[StatId.FromRecord(stat.Stat)].Name}");
                    break;
                case SkillEffect skill:
                    parts.Add($"{Signed(skill.Amount.At(0))} {member.Rules[SkillId.FromRecord(skill.Skill)].Name}");
                    break;
                case ProtectionEffect protection:
                    parts.Add($"{Signed(protection.Amount.At(0))} Protection");
                    break;
                case WardEffect ward:
                    parts.Add($"{Signed(ward.Amount.At(0))} vs. {ward.School}");
                    break;
                case HealEffect heal:
                    parts.Add(heal.Low == heal.High ? $"Heals {Num(heal.Low)}%" : $"Heals {Num(heal.Low)}-{Num(heal.High)}%");
                    break;
                case StaminaEffect stamina:
                    parts.Add(stamina.ToBase ? "Restores Stamina" : $"{Signed(stamina.Amount.At(0))} Stamina");
                    break;
                case CureEffect:
                    parts.Add("Cures");
                    break;
                case DamageEffect or BlastEffect:
                    if (!parts.Contains("Damage"))
                        parts.Add("Damage");
                    break;
            }
        }
        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static string Signed(int value) => (value > 0 ? "+" : "") + Num(value);

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
}
