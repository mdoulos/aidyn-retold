using AidynRetold.Core.Calendar;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Magic;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>The remade game's hunger and its Cook skill (the user's design, Nov 4, 2026).</summary>
public class HungerTests
{
    private static readonly ItemRecord Apple = ItemData.NewItem(0x2001, ItemKind.Food) with
    {
        Id = RecordId.Parse("acre:item/0x2001"), Name = "Apple",
        Effects = [new StaminaEffect(new Magnitude(4, 0))], Use = PotionUse.DrinkInTrek | PotionUse.DrinkInBattle,
    };

    /// <summary>A meal: a cooked dish, which counts toward Well Fed.</summary>
    private static readonly ItemRecord Stew = ItemData.NewItem(0x2002, ItemKind.Food, traits: new ItemTraits(OwnSlot: true, Meal: true)) with
    {
        Id = RecordId.Parse("acre:item/0x2002"), Name = "Stew",
        Effects = [new HealEffect(25, 35)], Use = PotionUse.DrinkInTrek | PotionUse.DrinkInBattle,
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

    [Fact]
    public void AMemberGrowsHungryAfterTwoDaysAndStarvingAfterThreeAndEatingFeedsThem()
    {
        var member = Member();
        var before = Four.Select(s => member.Stats.Modified(s)).ToList();

        member.AgeEffects(Days(1.9));
        Assert.Equal(HungerState.Fed, member.Hunger);

        member.AgeEffects(Days(0.2));
        Assert.Equal(HungerState.Hungry, member.Hunger);
        Assert.Equal(before.Select(v => v - Character.HungryLoss), Four.Select(s => member.Stats.Modified(s)));

        member.AgeEffects(Days(1));
        Assert.Equal(HungerState.Starving, member.Hunger);
        Assert.Equal(before.Select(v => v - Character.StarvingLoss), Four.Select(s => member.Stats.Modified(s)));
        // Endurance and Stamina, hit points, aren't touched.
        Assert.Equal(member.MaxHp, member.Hp);

        member.Eat();
        Assert.Equal(HungerState.Fed, member.Hunger);
        Assert.Equal(before, Four.Select(s => member.Stats.Modified(s)));
    }

    [Fact]
    public void CampingsHoursMakeAMemberHungrier()
    {
        var member = Member();
        // Six camps of eight hours: two days.
        for (var i = 0; i < 6; i++)
            member.AgeEffects(24_000);
        Assert.Equal(HungerState.Hungry, member.Hunger);
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
        Assert.Equal(0ul, member.SinceMeal);
    }

    [Fact]
    public void EatingFoodFeedsTheEaterEvenAtFullHealth()
    {
        var party = new Party(ItemData.Catalog(Apple));
        var member = Member();
        party.Add(member);
        party.Inventory.Add(Apple.Id);
        member.AgeEffects(Days(2.5));
        Assert.Equal(HungerState.Hungry, member.Hunger);

        var food = party.Inventory.Catalog.TryGet(Apple.Id)!;
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, food, new Rng.Lcg()));
        Assert.Equal(HungerState.Fed, member.Hunger);
        Assert.False(party.Inventory.Has(Apple.Id));
    }

    [Fact]
    public void TwoMealsMakeAMemberWellFedWhoCantEatUntilItWearsOff()
    {
        var party = new Party(ItemData.Catalog(Apple, Stew));
        var member = Member();
        party.Add(member);
        for (var i = 0; i < 3; i++)
            party.Inventory.Add(Stew.Id);
        party.Inventory.Add(Apple.Id);
        var stew = party.Inventory.Catalog.TryGet(Stew.Id)!;
        var apple = party.Inventory.Catalog.TryGet(Apple.Id)!;
        var random = new Rng.Lcg();
        Assert.False(member.IsFed);

        // One meal: Fed, which hides, and shows as nothing.
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, stew, random));
        Assert.True(member.IsFed);
        Assert.Equal(HungerState.Fed, member.Hunger);

        // Two: Well Fed, which shows, and nothing more goes down, a snack neither.
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, stew, random));
        Assert.Equal(HungerState.WellFed, member.Hunger);
        Assert.Equal(DrinkResult.WellFed, Potions.CanDrink(member, stew));
        Assert.Equal(DrinkResult.WellFed, Potions.CanDrink(member, apple, inBattle: true));
        Assert.Equal(DrinkResult.WellFed, Potions.Drink(party, member, apple, random));
        Assert.True(party.Inventory.Has(Apple.Id));

        // A third of a day on, it's worn off, and the meals are forgotten: eating starts over.
        member.AgeEffects(Days(0.34));
        Assert.Equal(HungerState.Fed, member.Hunger);
        Assert.False(member.IsFed);
        Assert.Equal(DrinkResult.Drunk, Potions.Drink(party, member, apple, random));
        Assert.False(member.IsFed);
    }

    [Fact]
    public void ASnackIsNoMealAndASaveKeepsTheMeals()
    {
        var party = new Party(ItemData.Catalog(Apple, Stew));
        var member = Member();
        party.Add(member);
        party.Inventory.Add(Apple.Id);
        party.Inventory.Add(Apple.Id);
        var apple = party.Inventory.Catalog.TryGet(Apple.Id)!;
        Potions.Drink(party, member, apple, new Rng.Lcg());
        Potions.Drink(party, member, apple, new Rng.Lcg());
        Assert.False(member.IsFed);
        Assert.Equal(HungerState.Fed, member.Hunger);

        member.SetMeals(2, 100);
        Assert.Equal(HungerState.WellFed, member.Hunger);
        Assert.Equal((2, 100ul), (member.Meals, member.SinceMealEaten));
        member.SetMeals(2, Character.FullFor);
        Assert.Equal(0, member.Meals);
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
