using AidynRetold.Core.Calendar;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Magic;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>The remade game's sleep (the user's design, Nov 5, 2026): Tired a day and a half after sleeping, Overtired after three, until a camp.</summary>
public class SleepTests
{
    private static readonly ItemRecord Apple = ItemData.NewItem(0x2201, ItemKind.Food) with
    {
        Id = RecordId.Parse("acre:item/0x2201"), Name = "Apple", Effects = [new StaminaEffect(new Magnitude(4, 0))], Use = PotionUse.DrinkInTrek, Satiety = 1,
    };

    /// <summary>Rules with food among the items, which hunger and sleep both need.</summary>
    private static readonly CharacterRules Remade = new(Characters.Rules.Stats, Characters.Rules.Skills, Characters.Rules.Progression)
    {
        Magic = new MagicRules(MagicData.Record, MagicData.Spells, [.. Enum.GetValues<BasePotion>().Select(PotionData.Of), Apple], MagicData.Inventory),
    };

    private static readonly Stat[] Four = [Stat.Intelligence, Stat.Willpower, Stat.Dexterity, Stat.Strength];

    private static Character Member() => Character.Create(Characters.Alaron(), Remade, _ => Characters.Fireball);

    private static int Days(double days) => (int)(days * GameClock.Day / GameClock.DaySpeed);

    [Fact]
    public void AMemberStartsRestedTiresAfterADayAndAHalfAndIsOvertiredAfterThree()
    {
        var member = Member();
        var before = Four.Select(s => member.Stats.Modified(s)).ToList();
        Assert.Equal(RestState.Rested, member.Rest);
        Assert.Equal(0UL, member.SinceSlept);

        // Fed along the way, so hunger's costs stay out of it.
        member.AgeEffects(Days(1.4));
        Assert.Equal(RestState.Rested, member.Rest);
        member.AgeEffects(Days(0.1));
        member.Eat(3);
        Assert.Equal((RestState.Tired, HungerState.Fed), (member.Rest, member.Hunger));
        Assert.Equal(before.Select(v => v - Character.TiredLoss), Four.Select(s => member.Stats.Modified(s)));

        member.AgeEffects(Days(1.5));
        member.Eat(3);
        member.Eat(3);
        Assert.Equal((RestState.Overtired, HungerState.Fed), (member.Rest, member.Hunger));
        Assert.Equal(before.Select(v => v - Character.OvertiredLoss), Four.Select(s => member.Stats.Modified(s)));
        Assert.Equal(member.MaxHp, member.Hp);

        // Only sleep brings it back, and entirely.
        member.AgeEffects(Days(5));
        Assert.Equal(RestState.Overtired, member.Rest);
        member.Sleep();
        member.Eat(3);
        member.Eat(3);
        member.Eat(3);
        member.Eat(3);
        Assert.Equal((RestState.Rested, HungerState.WellFed), (member.Rest, member.Hunger));
        Assert.Equal(before, Four.Select(s => member.Stats.Modified(s)));
    }

    [Fact]
    public void HungerAndTirednessCostTheStatsSeparatelyAndTogether()
    {
        var member = Member();
        var before = member.Stats.Modified(Stat.Strength);
        member.AgeEffects(Days(1.6));
        Assert.Equal((HungerState.Hungry, RestState.Tired), (member.Hunger, member.Rest));
        Assert.Equal(before - Character.HungryLoss - Character.TiredLoss, member.Stats.Modified(Stat.Strength));
        member.Sleep();
        Assert.Equal(before - Character.HungryLoss, member.Stats.Modified(Stat.Strength));
        member.Eat(3);
        Assert.Equal(before, member.Stats.Modified(Stat.Strength));
    }

    [Fact]
    public void CampingSleepsTheLivingThroughItsHours()
    {
        var party = new Party(ItemData.Catalog(Apple));
        var member = Member();
        party.Add(member);
        member.AgeEffects(Days(2));
        Assert.Equal(RestState.Tired, member.Rest);
        party.CampHeal(0.5f, 24_000, new Rng.Lcg());
        Assert.Equal(RestState.Rested, member.Rest);
        Assert.Equal(0UL, member.SinceSlept);
        // Hunger isn't fed by sleeping: the camp's hours cost it a point.
        Assert.Equal(2, member.Satiety);
    }

    [Fact]
    public void WithNoFoodInThePluginsNoOneTires()
    {
        var member = Character.Create(Characters.Alaron(), Characters.Rules, _ => Characters.Fireball);
        member.AgeEffects(Days(5));
        Assert.Equal(RestState.Rested, member.Rest);
    }

    [Fact]
    public void ASaveKeepsTheTimeSinceSleepingAndAnOlderOneIsRested()
    {
        var member = Member();
        member.SetSinceSlept(Character.TiredAfter + 5);
        Assert.Equal((Character.TiredAfter + 5, RestState.Tired), (member.SinceSlept, member.Rest));
        Assert.Equal(Character.TiredAfter + 5, CharacterSaves.Save(member).SinceSlept);
        // A save from before sleep has no time since sleeping, which loads as rested, as a fresh member saves.
        Assert.Equal(0UL, CharacterSaves.Save(Member()).SinceSlept);
        member.SetSinceSlept(ulong.MaxValue);
        Assert.Equal(RestState.Overtired, member.Rest);
        Assert.True(member.SinceSlept < ulong.MaxValue);
    }

    [Fact]
    public void TheWorseOfHungerAndTirednessPulsesNeverBoth()
    {
        Assert.Null(StatusPulse.Color(HungerState.Fed, RestState.Rested));
        Assert.Null(StatusPulse.Color(HungerState.WellFed, RestState.Rested));
        Assert.Equal(HungerDisplay.Hungry, StatusPulse.Color(HungerState.Fed, RestState.Tired));
        Assert.Equal(HungerDisplay.Hungry, StatusPulse.Color(HungerState.Hungry, RestState.Tired));
        Assert.Equal(HungerDisplay.Starving, StatusPulse.Color(HungerState.Hungry, RestState.Overtired));
        Assert.Equal(HungerDisplay.Starving, StatusPulse.Color(HungerState.Starving, RestState.Rested));
        Assert.Equal(HungerDisplay.Tint(HungerState.Starving, 0.4f), StatusPulse.Tint(HungerState.Hungry, RestState.Overtired, 0.4f));
        Assert.Equal(RestDisplay.Tint(RestState.Tired, 0.4f), StatusPulse.Tint(HungerState.Fed, RestState.Tired, 0.4f));
        Assert.Null(StatusPulse.Tint(HungerState.Fed, RestState.Rested, 0.4f));
        Assert.Equal(RestDisplay.TiredIcon, RestDisplay.StatusIcon(RestState.Tired));
        Assert.Null(RestDisplay.StatusIcon(RestState.Rested));
    }

    [Fact]
    public void TheHudShowsAHurtMembersHealthBarAndAStatusDot()
    {
        var portraits = new PartyPortraits();
        for (var i = 0; i < 14; i++)
            portraits.StepFrame();
        var face = RecordId.Hex(RecordTypes.Image, 0x0125);
        var whole = portraits.Draw([new HudMember(face)], 1).ToList();
        Assert.DoesNotContain(whole, s => s.Image == PartyPortraits.Pixel || s.Image == PartyPortraits.Dot);

        var hurt = portraits.Draw([new HudMember(face, Health: 0.5f)], 1).ToList();
        var bar = hurt.Where(s => s.Image == PartyPortraits.Pixel).ToList();
        Assert.Equal(2, bar.Count);
        // The backing along the portrait's bottom, 2 in from each side, and the fill half as wide.
        Assert.Equal((92f, 216f, (26f, 2f)), (bar[0].X, bar[0].Y, bar[0].Size!.Value));
        Assert.Equal((13f, 2f), bar[1].Size!.Value);
        Assert.Equal(PartyPortraits.BarFill, bar[1].Color);
        Assert.DoesNotContain(hurt, s => s.Image == PartyPortraits.Dot);

        var low = portraits.Draw([new HudMember(face, Health: 0.2f)], 1).ToList();
        Assert.Equal(PartyPortraits.BarLow, low.Last(s => s.Image == PartyPortraits.Pixel).Color);

        // Tired: the amber dot in the bottom right corner, above the bar's place, and the pulse over the portrait.
        var tired = portraits.Draw([new HudMember(face, Rest: RestState.Tired)], 1).ToList();
        var dot = tired.Single(s => s.Image == PartyPortraits.Dot);
        Assert.Equal((113f, 210f, (5f, 5f), HungerDisplay.Hungry), (dot.X, dot.Y, dot.Size!.Value, dot.Color));
        Assert.Equal(2, tired.Count(s => s.Image == face));
        var starving = portraits.Draw([new HudMember(face, HungerState.Starving, RestState.Tired)], 1).Single(s => s.Image == PartyPortraits.Dot);
        Assert.Equal(HungerDisplay.Starving, starving.Color);
    }
}
