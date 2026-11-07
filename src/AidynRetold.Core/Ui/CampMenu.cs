using System.Globalization;
using AidynRetold.Core.Camp;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Core.Rng;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>How the camp screen was left.</summary>
public enum CampChoice
{
    /// <summary>It's still up.</summary>
    None,
    /// <summary>Rest: the party heals, sleeps and its hours pass, and the night's scene plays.</summary>
    Rest,
    /// <summary>Break camp: back to trek with nothing done, as B asks.</summary>
    Leave,
}

/// <summary>The camp screen's tabs, down its left side, the user's design (Nov 7, 2026).</summary>
public enum CampTab
{
    /// <summary>The party round the fire: each member's portrait, health and statuses, and Rest.</summary>
    Camp,
    /// <summary>The Cook's recipes.</summary>
    Food,
    /// <summary>The Alchemist's recipes.</summary>
    Potions,
    /// <summary>Nothing yet.</summary>
    Weapons,
    /// <summary>The Mechanic's recipes, armor from hides.</summary>
    Armor,
}

/// <summary>
/// The camp screen, the remade game's (the user's design, Nov 7, 2026): the party round the fire (<see cref="Arrangement"/>) under
/// five tabs, Camp, Food, Potions, Weapons and Armor. Camp shows the members and Rest, which says how many hours the night will
/// take; Food, Potions and Armor offer the Cook's, the Alchemist's and the Mechanic's recipes (<see cref="CampActivityRecord"/>), made
/// by the member best at the skill, and each thing made adds its activity's hours to the rest, so the more is made, the later the
/// party wakes. Weapons holds nothing yet. Positions are the view's; the core keeps the state, as it does for the pause menu's tabs.
/// </summary>
/// <remarks>
/// The triggers, or left and right, change the tab; up and down move over a tab's recipes; A makes the cursor's recipe, or on Camp
/// rests; C-left and C-right change who makes it among those with the skill; B breaks camp with nothing done; Start opens the pause
/// menu over the camp. A controller and a mouse both reach everything: the view points at a tab or a row under the mouse and
/// clicks as A does.
/// </remarks>
public sealed class CampMenu
{
    /// <summary>The camp screen's own lines, the remade game's, as the original has no camp screen.</summary>
    public const string RestText = "Rest", RestHoursText = "Rest ({0} hours)", LeaveText = "Break camp", NobodyCan = "Nobody has the skill.",
        NothingToMake = "Nothing to make.", NothingHere = "", Ready = "{0} is ready.", GoesWrong = "It goes wrong: {0}.", Fails = "{0} fails.",
        MissingIngredients = "You don't have the ingredients.", StackFull = "You can't carry any more of it.", TooLow = "{0} isn't skilled enough.",
        MakerNote = "{0} makes it (C-left, C-right to change)", HoursNote = "+{0} h each";

    /// <summary>The tabs in order, down the left side.</summary>
    public static readonly IReadOnlyList<CampTab> Tabs = [CampTab.Camp, CampTab.Food, CampTab.Potions, CampTab.Weapons, CampTab.Armor];

    private readonly Party _party;
    private readonly IReadOnlyList<RecipeRecord> _recipes;
    private readonly IRandom _random;
    private readonly List<string> _log = [];
    private readonly Dictionary<string, int> _made = [];
    private int _added;

    /// <param name="site">The campsite shown, or null where the plugins have none.</param>
    /// <param name="arrangement">The party's arrangement at it, or null with no campsite.</param>
    /// <param name="offers">The activities, in order, which the tabs find by their skills.</param>
    /// <param name="recipes">Every recipe, which an activity's are chosen from again as ingredients are used.</param>
    /// <param name="baseHours">The rest's hours before anything made adds to them (<see cref="CampingRecord.RestHours"/>).</param>
    /// <param name="random">The stream the crafting rolls on.</param>
    public CampMenu(Party party, CampsiteRecord? site, CampArrangement? arrangement, IReadOnlyList<CampOffer> offers, IReadOnlyList<RecipeRecord> recipes,
        int baseHours, IRandom random)
    {
        _party = party;
        _recipes = recipes;
        _random = random;
        Site = site;
        Arrangement = arrangement;
        Offers = offers;
        BaseHours = baseHours;
    }

    public CampsiteRecord? Site { get; }

    public CampArrangement? Arrangement { get; }

    /// <summary>The camp's seed (<see cref="CampSeed"/>), which the arrangement was chosen by and the night's talk was.</summary>
    public uint Seed { get; init; }

    public IReadOnlyList<CampOffer> Offers { get; private set; }

    /// <summary>The party's members, in slot order, for the Camp tab's portraits.</summary>
    public IReadOnlyList<Character> Members => [.. _party.Members];

    /// <summary>The rest's hours before anything made adds to them.</summary>
    public int BaseHours { get; }

    /// <summary>The hours the rest will take: the base and what's been made.</summary>
    public int Hours => BaseHours + _added;

    /// <summary>What the Rest button says.</summary>
    public string RestLabel => string.Format(CultureInfo.InvariantCulture, RestHoursText, Hours);

    /// <summary>The tab showing.</summary>
    public CampTab Tab { get; private set; }

    /// <summary>The activity a tab offers: Food the Cook's, Potions the Alchemist's, Armor the Mechanic's; null for Camp and Weapons.</summary>
    public CampOffer? Offer(CampTab tab) => SkillOf(tab) is { } skill ? Offers.FirstOrDefault(o => o.Activity.Skill == skill) : null;

    /// <summary>The tab's activity, if it has one.</summary>
    public CampOffer? Current => Offer(Tab);

    /// <summary>Who makes the tab's recipes: its best member, or another with the skill the player chose.</summary>
    public Character? Member { get; private set; }

    /// <summary>The recipes the member can make now, on the tab showing.</summary>
    public IReadOnlyList<RecipeRecord> Recipes { get; private set; } = [];

    /// <summary>The cursor's recipe on the tab showing.</summary>
    public int RecipeCursor { get; private set; }

    /// <summary>What the tab says when it offers nothing: nobody has the skill, there's nothing to make, or it's empty for now.</summary>
    public string? Empty => Tab switch
    {
        CampTab.Camp => null,
        CampTab.Weapons => NothingHere,
        _ => Current is not { } offer || offer.Member is null ? NobodyCan : Recipes.Count == 0 ? NothingToMake : null,
    };

    /// <summary>The last thing said: what was made, or why nothing was.</summary>
    public string? Message { get; private set; }

    /// <summary>Everything said this camp, in order.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>What's been made this camp, by name, with how many.</summary>
    public IReadOnlyDictionary<string, int> Made => _made;

    /// <summary>How the screen was left, once it was.</summary>
    public CampChoice Choice { get; private set; }

    /// <summary>Whether a talk shows over the screen, in the conversation box, which takes the presses meanwhile; the tabs are hidden under it.</summary>
    public bool Talking { get; set; }

    /// <summary>How many talks the debug button has started this camp, which each press's seed counts on (<c>GameSession.TriggerCampTalk</c>).</summary>
    public int TalksTriggered { get; set; }

    /// <summary>The name of a recipe's product, for the rows.</summary>
    public string ProductName(RecipeRecord recipe) => _party.Inventory.Catalog.TryGet(recipe.Product)?.Name ?? recipe.Product.ToString();

    /// <summary>The skill a tab's recipes are: the Cook's, the Alchemist's or the Mechanic's record.</summary>
    public static RecordId? SkillOf(CampTab tab) => tab switch
    {
        CampTab.Food => Crafting.Cook,
        CampTab.Potions => Crafting.Alchemist,
        CampTab.Armor => Crafting.Mechanic,
        _ => null,
    };

    /// <summary>A press on the screen; whether it took it.</summary>
    public bool Press(MenuAction action)
    {
        if (Choice != CampChoice.None || Talking)
            return false;
        switch (action)
        {
            case MenuAction.TabPrevious or MenuAction.Left:
                Show(Tabs[(Tabs.ToList().IndexOf(Tab) + Tabs.Count - 1) % Tabs.Count]);
                return true;
            case MenuAction.TabNext or MenuAction.Right:
                Show(Tabs[(Tabs.ToList().IndexOf(Tab) + 1) % Tabs.Count]);
                return true;
            case MenuAction.Up or MenuAction.Down:
                if (Recipes.Count > 0)
                    RecipeCursor = Math.Clamp(RecipeCursor + action.Vertical(), 0, Recipes.Count - 1);
                return true;
            case MenuAction.MemberLeft or MenuAction.MemberRight:
                if (Current is { } offer && Member is { } current)
                {
                    var able = CampActivities.Able(_party, offer.Skill).ToList();
                    if (able.Count > 1)
                    {
                        var at = able.IndexOf(current);
                        Member = able[(at + (action == MenuAction.MemberRight ? 1 : able.Count - 1)) % able.Count];
                        RefreshRecipes();
                    }
                }
                return true;
            case MenuAction.Accept:
                if (Tab == CampTab.Camp)
                {
                    Choice = CampChoice.Rest;
                    return true;
                }
                if (Current is { } making && Member is { } member && Recipes.ElementAtOrDefault(RecipeCursor) is { } recipe)
                    Make(making, member, recipe);
                else if (Empty is { Length: > 0 } why)
                    Say(why);
                return true;
            case MenuAction.Back:
                Choice = CampChoice.Leave;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Shows a tab, its recipes from its best member, as a press or the mouse asks.</summary>
    public void Show(CampTab tab)
    {
        Tab = tab;
        Member = Current?.Member;
        RecipeCursor = 0;
        Message = null;
        RefreshRecipes();
    }

    /// <summary>The cursor onto a recipe of the tab showing, as the mouse points at one; false if there's none.</summary>
    public bool PointRecipe(int row)
    {
        if (row < 0 || row >= Recipes.Count)
            return false;
        RecipeCursor = row;
        return true;
    }

    /// <summary>Makes the recipe, says how it went, and adds the activity's hours for anything made, a spoilt dish among them.</summary>
    private void Make(CampOffer offer, Character member, RecipeRecord recipe)
    {
        var name = ProductName(recipe);
        var result = Crafting.Make(_party, member, recipe, _random);
        switch (result)
        {
            case CraftResult.Made:
                Say(string.Format(CultureInfo.InvariantCulture, Ready, name));
                Count(name);
                _added += offer.Activity.Hours;
                break;
            case CraftResult.Spoilt:
                var spoilt = recipe.Failure is { } failure ? _party.Inventory.Catalog.TryGet(failure)?.Name ?? failure.ToString() : name;
                Say(string.Format(CultureInfo.InvariantCulture, GoesWrong, spoilt));
                Count(spoilt);
                _added += offer.Activity.Hours;
                break;
            case CraftResult.MissingIngredients:
                Say(MissingIngredients);
                break;
            case CraftResult.StackFull:
                Say(StackFull);
                break;
            case CraftResult.TooLow:
                Say(string.Format(CultureInfo.InvariantCulture, TooLow, member.Name));
                break;
            default:
                Say(string.Format(CultureInfo.InvariantCulture, Fails, member.Name));
                break;
        }
        RefreshRecipes();
    }

    private void Count(string name) => _made[name] = _made.GetValueOrDefault(name) + 1;

    /// <summary>Every offer's recipes again, as the ingredients stand now, and the tab's from its member, the cursor kept within them.</summary>
    private void RefreshRecipes()
    {
        Offers = [.. Offers.Select(o => o with { Recipes = CampActivities.Recipes(_party, o.Member, o.Activity, _recipes) })];
        Recipes = Current is { } offer && Member is not null ? CampActivities.Recipes(_party, Member, offer.Activity, _recipes) : [];
        RecipeCursor = Recipes.Count == 0 ? 0 : Math.Min(RecipeCursor, Recipes.Count - 1);
    }

    private void Say(string text)
    {
        Message = text;
        _log.Add(text);
    }
}
