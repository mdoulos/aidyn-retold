using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Characters;

/// <summary>The attributes and the level, as the original numbers them (<c>StatEnum</c>).</summary>
public enum Stat
{
    Intelligence,
    Willpower,
    Dexterity,
    Endurance,
    Strength,
    Stamina,
    /// <summary>The level. It's a stat because its points are hit points too, and the first damage takes.</summary>
    Level,
}

/// <summary>The skills, as the original numbers them (<c>SkillEnum</c>), and the remade game's Cook after them.</summary>
public enum Skill
{
    Alchemist,
    Diplomat,
    Healer,
    Loremaster,
    Mechanic,
    Merchant,
    Ranger,
    Stealth,
    Thief,
    Troubadour,
    Warrior,
    Wizard,
    /// <summary>
    /// The remade game's: cooking food from ingredients, as the Alchemist brews potions (<see cref="Items.Crafting"/>). Its record is
    /// the ACRE plugin's, <c>acre:skill/12</c>; a character holds it after the shield (<see cref="SkillId.Cook"/>).
    /// </summary>
    Cook,
}

/// <summary>The stat records (<c>aidyn:stat/0</c>–<c>6</c>) by <see cref="Stat"/>.</summary>
public static class StatId
{
    public static RecordId Record(Stat stat) => RecordId.Decimal(RecordTypes.Stat, (int)stat);

    /// <summary>The stat a record names.</summary>
    public static Stat FromRecord(RecordId record) => record.Type == RecordTypes.Stat
        ? (Stat)int.Parse(record.Key)
        : throw new ArgumentException($"{record} isn't a stat.", nameof(record));
}

/// <summary>
/// A skill, a weapon skill or the shield skill, by where a character holds it (<c>CharSkills</c>): the twelve skills, then the
/// eleven weapon classes, then the shield, as the original holds them; then the remade game's Cook, so the original's 24 keep their
/// places in its saves and checkpoints.
/// </summary>
public readonly record struct SkillId(int Slot)
{
    public const int Count = SkillSlots.Count;

    public static SkillId Shield => new(23);

    /// <summary>The remade game's Cook, after the shield.</summary>
    public static SkillId Cook => new(SkillSlots.Cook);

    /// <summary>Cook's record, the ACRE plugin's: the skills' records run on from the original's twelve.</summary>
    public static readonly RecordId CookRecord = RecordId.Parse($"{AcrePlugin.Id}:skill/12");

    public static implicit operator SkillId(Skill skill) => new(skill == Skill.Cook ? SkillSlots.Cook : (int)skill);

    public static implicit operator SkillId(WeaponClass weapon) => new(12 + (int)weapon);

    public bool IsSkill => Slot < 12 || Slot == SkillSlots.Cook;

    public bool IsWeapon => Slot is >= 12 and < 23;

    public bool IsShield => Slot == 23;

    public Skill Skill => Slot == SkillSlots.Cook ? Skill.Cook : IsSkill ? (Skill)Slot : throw new InvalidOperationException($"{this} isn't a skill.");

    public WeaponClass Weapon => IsWeapon ? (WeaponClass)(Slot - 12) : throw new InvalidOperationException($"{this} isn't a weapon skill.");

    /// <summary><c>aidyn:skill/n</c>, or <c>aidyn:weaponskill/n</c>, where the shield is 11; Cook's <see cref="CookRecord"/>.</summary>
    public RecordId Record => Slot == SkillSlots.Cook ? CookRecord
        : IsSkill ? RecordId.Decimal(RecordTypes.Skill, Slot) : RecordId.Decimal(RecordTypes.WeaponSkill, Slot - 12);

    /// <summary>Every slot, in the order a character holds them.</summary>
    public static IEnumerable<SkillId> All => Enumerable.Range(0, Count).Select(s => new SkillId(s));

    /// <summary>
    /// The order the skill screen and training list them in: the skills by name, as the original's twelve are, Cook after the
    /// Alchemist, then the weapon skills and the shield.
    /// </summary>
    public static IEnumerable<SkillId> Shown => [new(0), Cook, .. Enumerable.Range(1, SkillSlots.OriginalCount - 1).Select(s => new SkillId(s))];

    /// <summary>The skill a record names: <c>aidyn:skill/n</c> or <c>aidyn:weaponskill/n</c>, Cook's <c>12</c>.</summary>
    public static SkillId FromRecord(RecordId record) => record.Type switch
    {
        RecordTypes.Skill => int.Parse(record.Key) is var n && n == 12 ? Cook : new(n),
        RecordTypes.WeaponSkill => new(12 + int.Parse(record.Key)),
        _ => throw new ArgumentException($"{record} isn't a skill.", nameof(record)),
    };

    public override string ToString() => IsSkill ? Skill.ToString() : IsWeapon ? Weapon.ToString() : "Shield";
}

/// <summary>
/// The tables characters are built and trained by: the stat, skill, weapon skill and progression
/// records (<c>CharSkills::GetSkillXpMod</c> and so on).
/// </summary>
public sealed class CharacterRules
{
    public const int StatCount = 7;

    public CharacterRules(IReadOnlyList<StatRecord> stats, IReadOnlyList<SkillRecord> skills, ProgressionRecord progression)
    {
        if (stats.Count != StatCount)
            throw new ArgumentException($"There are {StatCount} stats, Intelligence to Level.", nameof(stats));
        // The original's 24 have Cook added, as no plugin gives it.
        if (skills.Count == SkillSlots.OriginalCount)
            skills = [.. skills, NoCook];
        if (skills.Count != SkillId.Count)
            throw new ArgumentException($"There are {SkillId.Count} skills, weapon skills, the shield and Cook.", nameof(skills));
        if (progression.Levels is not [0, ..])
            throw new ArgumentException("Level 0 starts at 0 experience.", nameof(progression));
        Stats = stats;
        Skills = skills;
        Progression = progression;
    }

    /// <summary>Stats 0–6 by <see cref="Stat"/>.</summary>
    public IReadOnlyList<StatRecord> Stats { get; }

    /// <summary>Skills, weapon skills and the shield by <see cref="SkillId.Slot"/>.</summary>
    public IReadOnlyList<SkillRecord> Skills { get; }

    public ProgressionRecord Progression { get; }

    /// <summary>What spells, potions and enchantments are made of.</summary>
    public required Magic.MagicRules Magic { get; init; }

    public StatRecord this[Stat stat] => Stats[(int)stat];

    public SkillRecord this[SkillId skill] => Skills[skill.Slot];

    public TrainingPrices Training => Progression.Training;

    /// <summary>The rules from the loaded plugins.</summary>
    public static CharacterRules From(PluginSet plugins) => new(
        [.. Enumerable.Range(0, StatCount).Select(s => plugins.Get<StatRecord>(StatId.Record((Stat)s)))],
        [.. SkillId.All.Select(s => s == SkillId.Cook ? plugins.TryGet<SkillRecord>(s.Record) ?? NoCook : plugins.Get<SkillRecord>(s.Record))],
        plugins.Singleton<ProgressionRecord>())
    {
        Magic = AidynRetold.Core.Magic.MagicRules.From(plugins),
    };

    /// <summary>
    /// Cook where no plugin gives its record, as with the original game alone: a skill no one has, so nothing lists or teaches it.
    /// </summary>
    private static readonly SkillRecord NoCook = new(SkillId.CookRecord, "Cook", null, 500, 10, 15);

    /// <summary>
    /// The level <paramref name="exp"/> reaches: the last whose threshold it's reached
    /// (<c>Entity::GetLevel</c>). Past the table, the last level.
    /// </summary>
    public int LevelFor(uint exp)
    {
        var levels = Progression.Levels;
        for (var level = 0; level < levels.Count; level++)
        {
            if (exp < (uint)levels[level])
                return (ushort)(level - 1);
        }
        return levels.Count - 1;
    }

    /// <summary>The experience the level after <paramref name="exp"/>'s starts at, or null past the last (<c>Entity::GetEXPTNL</c>).</summary>
    public uint? NextLevelExp(uint exp)
    {
        var next = (ushort)(LevelFor(exp) + 1);
        return next < Progression.Levels.Count ? (uint)Progression.Levels[next] : null;
    }

    /// <summary>
    /// What raising a stat, skill or spell from <paramref name="level"/> costs in experience: (level + 1)² × its
    /// <paramref name="cost"/> (<c>CharSkills::GetSkillXpMod</c>, <c>WidgetSpellTrain::Confirm</c>).
    /// </summary>
    public static int TrainingCost(int level, int cost) => (level + 1) * (level + 1) * cost;

    /// <summary>A trainer's gold price for raising a skill or spell from <paramref name="level"/> (<c>GetSkillTrainGoldPrice</c>).</summary>
    public int GoldPrice(int level) =>
        (byte)level != 0 ? ((byte)level + 1) * Training.GoldPerLevel : Training.FirstLevelGold;
}
