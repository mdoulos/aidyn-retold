using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Camp;

/// <summary>An activity as the camp screen offers it: who does it, the member best at its skill, and the recipes they can make now.</summary>
/// <param name="Member">The living member best at the skill, or null when nobody has it.</param>
public sealed record CampOffer(CampActivityRecord Activity, SkillId Skill, Character? Member, IReadOnlyList<RecipeRecord> Recipes)
{
    /// <summary>Whether it can be done now: someone has the skill and there's something they can make.</summary>
    public bool Available => Member is not null && Recipes.Count > 0;
}

/// <summary>What the party can do at camp (<see cref="CampActivityRecord"/>), by who has each skill and what its recipes offer them.</summary>
public static class CampActivities
{
    /// <summary>The activities in their order, each with its best member and their recipes. One whose skill no plugin has is left out.</summary>
    public static IReadOnlyList<CampOffer> Offered(IEnumerable<CampActivityRecord> activities, IReadOnlyList<RecipeRecord> recipes, Party party)
    {
        var offers = new List<CampOffer>();
        foreach (var activity in activities.OrderBy(a => a.Order).ThenBy(a => a.Id.ToString(), StringComparer.Ordinal))
        {
            if (SkillOf(activity) is not { } skill)
                continue;
            var member = Able(party, skill).OrderByDescending(m => m.Skills.Modified(skill)).FirstOrDefault();
            offers.Add(new CampOffer(activity, skill, member, Recipes(party, member, activity, recipes)));
        }
        return offers;
    }

    /// <summary>The recipes of an activity's skill a member can make now, or none with no member.</summary>
    public static IReadOnlyList<RecipeRecord> Recipes(Party party, Character? member, CampActivityRecord activity, IEnumerable<RecipeRecord> recipes) =>
        member is null ? [] : [.. Crafting.Offered(party, member, recipes.Where(r => r.Skill == activity.Skill))];

    /// <summary>The living members with the skill above 0, in party order.</summary>
    public static IEnumerable<Character> Able(Party party, SkillId skill) => party.Members.Where(m => !m.IsDead && m.Skills.Modified(skill) > 0);

    /// <summary>The skill an activity names, or null for a record that isn't a skill's.</summary>
    public static SkillId? SkillOf(CampActivityRecord activity)
    {
        try
        {
            return SkillId.FromRecord(activity.Skill);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
        {
            return null;
        }
    }
}
