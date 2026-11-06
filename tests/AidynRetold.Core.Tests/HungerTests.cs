using AidynRetold.Core.Calendar;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Magic;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>The remade game's hunger, its satiety and its Cook skill (the user's design, Nov 4 and 5, 2026).</summary>
public class HungerTests
{
    /// <summary>A snack, worth a point.</summary>
    private static readonly ItemRecord Apple = ItemData.NewItem(0x2001, ItemKind.Food) with
    {
        Id = RecordId.Parse("acre:item/0x2001"), Name = "Apple",
        Effects = [new StaminaEffect(new Magnitude(4, 0))], Use = PotionUse.DrinkInTrek | PotionUse.DrinkInBattle, Satiety = 1,
    };

    /// <summary>A dish of two ingredients, worth three.</summary>
    private static readonly ItemRecord Stew = ItemData.NewItem(0x2002, ItemKind.Food) with
    {
        Id = RecordId.Parse("acre:item/0x2002"), Name = "Stew",
        Effects = [new HealEffect(25, 35)], Use = PotionUse.DrinkInTrek | PotionUse.DrinkInBattle, Satiety = 3,
    };

    /// <summary>The test rules with food among the items, which hunger needs.</summary>
    private static readonly CharacterRules Fed = new(Characters.Rules.Stats, Characters.Rules.Skills, Characters.Rules.Progression)
    {
        Magic = new MagicRules(MagicData.Record, MagicData.Spells, [.. Enum.GetValues<BasePotion>().Select(PotionData.Of), Apple, Stew], MagicData.Inventory),
    };

    private static readonly Stat[] Four = [Stat.Intelligence, Stat.Willpower, Stat.Dexterity, Stat.Strength];

    private static Character Member(EntityRecord? entity = null) => Character.Create(entity ?? Characters.Alaron(), Fed, _ => Characters.Fireball);

    // Ticks of play for a number of the game's days: the clock runs 72 of its units a tick.
    private static int Days(double days) => (int)(days * GameClock.Day / GameClock.DaySpeed);

    private static (Party Party, Character Member, Item Apple, Item Stew) Table()
    {
        var party = new Party(ItemData.Catalog(Apple, Stew));
        var member = Member();
        party.Add(member);
        for (var i = 0; i < 3; i++)
        {
            party.Inventory.Add(Apple.Id);
            party.Inventory.Add(Stew.Id);
        }
        return (party, member, party.Inventory.Catalog.TryGet(Apple.Id)!, party.Inventory.Catalog.TryGet(Stew.Id)!);
    }

    [Fact]
    public void AMemberStartsFedAndLosesAPointEveryEightHoursHungryAfterADayAndStarvingAfterTwo()
    {
        var member = Member();
        var before = Four.Select(s => member.Stats.Modified(s)).ToList();
        Assert.Equal(Character.SatietyStart, member.Satiety);
        Assert.Equal(HungerState.Fed, member.Hunger);

        member.AgeEffects(Days(0.9));
        Assert.Equal(7, member.Satiety);
        Assert.Equal(HungerState.Fed, member.Hunger);

        member.AgeEffects(Days(0.2));
        Assert.Equal(6, member.Satiety);
        Assert.Equal(HungerState.Hungry, member.Hunger);
        Assert.Equal(before.Select(v => v - Character.HungryLoss), Four.Select(s => member.Stats.Modified(s)));

        member.AgeEffects(Days(1));
        // Slept, so tiredness's costs (SleepTests) stay out of it.
        member.Sleep();
        Assert.Equal(3, member.Satiety);
        Assert.Equal(HungerState.Starving, member.Hunger);
        Assert.Equal(before.Select(v => v - Character.StarvingLoss), Four.Select(s => member.Stats.Modified(s)));
        // Endurance and Stamina, hit points, aren't touched.
        Assert.Equal(member.MaxHp, member.Hp);

        // Nothing falls below one.
        member.AgeEffects(Days(3));
        member.Sleep();
        Assert.Equal(Character.SatietyLeast, member.Satiety);

        // Starving to Hungry takes three points, Hungry to Fed three more: a stew each.
        member.Eat(3);
        Assert.Equal(HungerState.Hungry, member.Hunger);
        member.Eat(3);
        Assert.Equal(HungerState.Fed, member.Hunger);
        Assert.Equal(before, Four.Select(s => member.Stats.Modified(s)));
    }

    [Fact]
    public void CampingsHoursTakeAPointEach()
    {
        var member = Member();
        // Six camps of eight hours: two days, six points.
        for (var i = 0; i < 6; i++)
            member.AgeEffects(24_000);
        Assert.Equal(3, member.Satiety);
        Assert.Equal(HungerState.Starving, member.Hunger);
    }

    [Fact]
    public void StarvingNeverTakesAStatBelowOne()
    {
        var member = Member(Characters.Alaron() with { Attributes = new Attributes(3, 12, 15, 15, 15, 30) });
        member.AgeEffects(Days(3));
        Assert.Equal(1, member.Stats.Modified(Stat.Intelligence));
    }

    [Fact]
    public void WithNoFoodInThePluginsNoOneGoesHungry()
    {
        var member = Character.Create(Characters.Alaron(), Characters.Rules, _ => Characters.Fireball);
        member.AgeEffects(Days(5));
        Assert.Equal(HungerState.Fed, member.Hunger);
        Assert.Equal(Character.SatietyStart, member.Satiety);
    }

    [Fact]
    public void FoodAddsItsPointsASnackOneADishMoreEvenAtFullHealth()
    {
        var (party, member, apple, stew) = Table();
        member.AgeEffects(Days(1.5));
        Assert.Equal(5, member.Satiety);
        Assert.Equal(HungerState.Hungry, member.Hunger);

        var random = new Rng.Lcg();
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, apple, random));
        Assert.Equal(6, member.Satiety);
        Assert.Equal(HungerState.Hungry, member.Hunger);
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, stew, random));
        Assert.Equal(9, member.Satiety);
        Assert.Equal(HungerState.Fed, member.Hunger);
        Assert.Equal(2, party.Inventory.Count(Apple.Id));
    }

    [Fact]
    public void WellFedFromNineStopsEatingUntilTimeBringsItDown()
    {
        var (party, member, apple, stew) = Table();
        var random = new Rng.Lcg();
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, stew, random));
        Assert.Equal(Character.SatietyMost, member.Satiety);
        Assert.Equal(HungerState.WellFed, member.Hunger);
        Assert.Equal(DrinkResult.WellFed, Potions.CanDrink(member, apple));
        Assert.Equal(DrinkResult.WellFed, Potions.CanDrink(member, stew, inBattle: true));
        Assert.Equal(DrinkResult.WellFed, Potions.Drink(party, member, apple, random));
        Assert.Equal(3, party.Inventory.Count(Apple.Id));

        // A day on, three points down: Fed, and eating again.
        member.AgeEffects(Days(1.01));
        Assert.Equal(9, member.Satiety);
        Assert.Equal(HungerState.Fed, member.Hunger);
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, apple, random));
        Assert.Equal(HungerState.WellFed, member.Hunger);
    }

    [Fact]
    public void ASaveKeepsTheSatietyAndAnOlderOneIsReadByItsTimeSinceAMeal()
    {
        var member = Member();
        member.SetSatiety(10, 100);
        Assert.Equal((10, 100ul, HungerState.WellFed), (member.Satiety, member.SinceFell, member.Hunger));
        member.SetSatiety(20, Character.FallEvery * 2);
        Assert.Equal((Character.SatietyMost, Character.FallEvery - 1), (member.Satiety, member.SinceFell));

        Assert.Equal(Character.SatietyStart, Character.SatietyOf(0));
        Assert.Equal(Character.FedFrom - 1, Character.SatietyOf(2UL * GameClock.Day));
        Assert.Equal(Character.HungryFrom - 1, Character.SatietyOf(3UL * GameClock.Day));
    }

    /// <summary>A spicy dish, which wards off Elemental magic for a while (the user's design, Nov 5, 2026).</summary>
    private static readonly ItemRecord SpicyStew = ItemData.NewItem(0x2003, ItemKind.Food) with
    {
        Id = RecordId.Parse("acre:item/0x2003"), Name = "Spicy Stew",
        Effects = [new HealEffect(25, 35), new WardEffect(MagicSchool.Elemental, new Magnitude(25))], Duration = new EffectDuration(Flat: 648_000),
        Use = PotionUse.DrinkInTrek, Satiety = 3,
    };

    [Fact]
    public void ASpicyDishWardsOffElementalMagicAndASecondNeitherStacksNorRefreshesIt()
    {
        var rules = new CharacterRules(Characters.Rules.Stats, Characters.Rules.Skills, Characters.Rules.Progression)
        {
            Magic = new MagicRules(MagicData.Record, MagicData.Spells, [.. Enum.GetValues<BasePotion>().Select(PotionData.Of), SpicyStew], MagicData.Inventory),
        };
        var party = new Party(ItemData.Catalog(SpicyStew));
        var member = Character.Create(Characters.Alaron(), rules, _ => Characters.Fireball);
        party.Add(member);
        party.Inventory.Add(SpicyStew.Id, 2);
        var stew = party.Inventory.Catalog.TryGet(SpicyStew.Id)!;
        member.AgeEffects(Days(1.5));
        Assert.Equal(0, Casting.Ward(member, MagicSchool.Elemental));

        var random = new Rng.Lcg();
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, stew, random));
        Assert.Equal(25, Casting.Ward(member, MagicSchool.Elemental));
        Assert.Single(member.PotionEffects);
        var timer = member.PotionEffects.Single().Timer;
        member.AgeEffects(Days(0.05));
        Assert.True(member.PotionEffects.Single().Timer < timer);

        // Eaten again while it lasts: fed, but one ward, with its time as it was.
        var left = member.PotionEffects.Single().Timer;
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, stew, random));
        Assert.Equal(11, member.Satiety);
        Assert.Single(member.PotionEffects);
        Assert.Equal(left, member.PotionEffects.Single().Timer);
        Assert.Equal(0, party.Inventory.Count(SpicyStew.Id));
    }

    /// <summary>Cook is held after the shield, so the original's 24 keep their places, and listed after the Alchemist.</summary>
    [Fact]
    public void CookIsHeldAfterTheShieldAndListedAfterTheAlchemist()
    {
        Assert.Equal(24, SkillId.Cook.Slot);
        Assert.Equal(SkillId.Cook, (SkillId)Skill.Cook);
        Assert.True(SkillId.Cook.IsSkill);
        Assert.Equal(Skill.Cook, SkillId.Cook.Skill);
        Assert.Equal(SkillId.Cook, SkillId.FromRecord(SkillId.CookRecord));
        Assert.Equal([new SkillId(0), SkillId.Cook, new SkillId(1)], SkillId.Shown.Take(3));
        Assert.Equal(SkillId.Count, SkillId.Shown.Distinct().Count());
        // The original's 24 levels, as its ROM and saves hold them, give Cook 0: anyone can learn it.
        var (skills, _, _) = SkillSlots.Split([.. Enumerable.Repeat<int?>(0, SkillSlots.OriginalCount)]);
        Assert.Equal(0, skills.Cook);
    }
}
