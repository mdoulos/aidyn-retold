using System.Globalization;
using AidynEnhanced.Core.Characters;
using AidynEnhanced.Core.Items;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>How an item in the grid stands with the member shown, which its cell marks (<see cref="PartyMenu.Mark"/>).</summary>
public enum ItemMark
{
    None,

    /// <summary>The member can't put it on, or learn its spell.</summary>
    Unusable,

    /// <summary>It would be better than what the member wears in its place.</summary>
    Upgrade,
}

/// <summary>
/// What the slip over an item says in the port's look (<see cref="PartyMenu.Card"/>): its name, a line of what it is, and why the member
/// shown can't use it, or what it would be better than.
/// </summary>
public sealed record ItemCard(string Name, string? About = null, string? Warning = null, string? Better = null);

/// <summary>
/// Marks on the grid's items for the member shown, in the port's look, the user's choice (Nov 3, 2026): what they can't use, dimmed with
/// a red cross, and what would be better than what they wear, with a green arrow; and the card over an item, which says why.
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>
    /// How an item stands with the member shown: <see cref="ItemMark.Unusable"/> if they can't put it on, or for a scroll they'd read,
    /// learn its spell; <see cref="ItemMark.Upgrade"/> if it would be better than what they wear in its place (<see cref="BetterThan"/>);
    /// none for what's worn, potions and the rest.
    /// </summary>
    public ItemMark Mark(InventoryRow row) => Shown is not { } member || row.Wearer is not null ? ItemMark.None
        : Refusal(member, row.Item) is not null ? ItemMark.Unusable
        : BetterThan(member, row.Item) is not null ? ItemMark.Upgrade
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
        if (item.Kind is ItemKind.Potion or ItemKind.Misc or ItemKind.Key)
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
    /// What a usable item would be better than, if it would: a weapon with more damage, or as much and more to hit, than the one wielded;
    /// armor with more protection; a shield with more defense and protection; a helmet, cloak, gloves, boots or belt with more protection
    /// and damage than the one worn. What it'd go in is the slot being chosen for, else what it would replace. Empty where nothing is
    /// worn there; null if it isn't better, or for rings, amulets, wands, scrolls and the rest, whose worth isn't one number.
    /// </summary>
    private string? BetterThan(Character member, Item item)
    {
        var worn = Picking is { } slot ? Worn(slot)?.Item : item.Kind switch
        {
            ItemKind.Weapon => member.Equipment.Weapon?.Item,
            ItemKind.Armor => member.Equipment.Armor?.Item,
            ItemKind.Shield => member.Equipment.Shield?.Item,
            _ => member.Equipment.Gear.OfType<ItemInstance>().FirstOrDefault(g => g.Item.Kind == item.Kind)?.Item,
        };
        (int, int)? Worth(Item what) => what.Kind switch
        {
            ItemKind.Weapon => (what.Weapon!.Damage, what.Weapon.Hit),
            ItemKind.Armor => (what.Armor!.Protection, 0),
            ItemKind.Shield => (what.Armor!.Defense + what.Armor.Protection, 0),
            ItemKind.Helmet or ItemKind.Cloak or ItemKind.Gloves or ItemKind.Boots or ItemKind.Belt => (what.Protection + what.Damage, 0),
            _ => null,
        };
        if (Worth(item) is not { } worth || worth == (0, 0))
            return null;
        if (worn is null)
            return "";
        return Worth(worn) is { } was && (worth.Item1 > was.Item1 || worth.Item1 == was.Item1 && worth.Item2 > was.Item2) ? worn.Name : null;
    }

    /// <summary>
    /// The card over an item, in the port's look: its name; a line of what it is, a weapon's skill, damage and to-hit, armor's
    /// protection, a shield's defense, gear's protection, damage and stat bonus, a scroll's spell; why the member shown can't use it,
    /// in red; or what it would be better than, in green. Just the name for what's worn.
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
            case ItemKind.Shield when item.Armor is { } shield:
                about.Add($"Defense {Num(shield.Defense)}");
                about.Add($"Protection {Num(shield.Protection)}");
                break;
            case ItemKind.Scroll when item.Bonuses.Spell is { } carried && _spells(carried.Spell) is { } spell:
                about.Add(spell.Name);
                break;
            case ItemKind.Potion:
                about.Add(Potions.IsFlask(item) ? "Flask" : "Potion");
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
        if (row.Wearer is not null)
            return new ItemCard(item.Name, about.Count > 0 ? string.Join(", ", about) : null);
        var warning = Refusal(member, item);
        var better = warning is null ? BetterThan(member, item) : null;
        return new ItemCard(item.Name, about.Count > 0 ? string.Join(", ", about) : null, warning, better switch
        {
            null => null,
            "" => "Nothing worn in its place",
            _ => $"Better than {better}",
        });
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
}
