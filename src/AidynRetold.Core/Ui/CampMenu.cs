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
    /// <summary>Rest: the party heals, sleeps and its hours pass, and the night's scene or talk plays.</summary>
    Rest,
    /// <summary>Break camp: back to trek with nothing done, as B at the top of the screen asks.</summary>
    Leave,
}

/// <summary>A row of the camp screen's list: an activity, or Rest last.</summary>
/// <param name="Offer">The activity, or null for Rest.</param>
/// <param name="Enabled">Whether A does something on it: an activity someone can do with something to make, or Rest.</param>
/// <param name="Note">What's said beside it: who does it and how much they can make, or why it can't be done.</param>
public sealed record CampRow(string Text, CampOffer? Offer, bool Enabled, string? Note);

/// <summary>
/// The camp screen, the remade game's (the user's design, Nov 7, 2026): the party round the fire (<see cref="Arrangement"/>) under a
/// list of what can be done before resting, cooking, brewing, making armor (<see cref="CampActivityRecord"/>), and Rest, which says
/// how many hours the night will take. Each thing made adds its activity's hours to the rest, so the more is made, the later the
/// party wakes. Positions are the view's; the core keeps the state, as it does for the pause menu's tabs.
/// </summary>
/// <remarks>
/// The stick moves the cursor over the rows; A opens an activity's recipes, or on Rest rests; B at the top breaks camp with nothing
/// done. In an activity the stick moves over the recipes, A makes the cursor's, C-left and C-right change who makes it among those
/// with the skill, and B goes back to the rows. A controller and a mouse both reach everything: the view points the cursor at a row
/// under the mouse and clicks as A does.
/// </remarks>
public sealed class CampMenu
{
    /// <summary>The camp screen's own lines, the remade game's, as the original has no camp screen.</summary>
    public const string RestText = "Rest", RestHoursText = "Rest ({0} hours)", LeaveText = "Break camp", NobodyCan = "Nobody has the skill.",
        NothingToMake = "Nothing to make.", Ready = "{0} is ready.", GoesWrong = "It goes wrong: {0}.", Fails = "{0} fails.",
        MissingIngredients = "You don't have the ingredients.", StackFull = "You can't carry any more of it.", TooLow = "{0} isn't skilled enough.",
        MadeNote = "{0}, {1} to make", HoursNote = "+{0} h each";

    private readonly Party _party;
    private readonly IReadOnlyList<RecipeRecord> _recipes;
    private readonly IRandom _random;
    private readonly List<string> _log = [];
    private readonly Dictionary<string, int> _made = [];
    private int _added;

    /// <param name="site">The campsite shown, or null where the plugins have none.</param>
    /// <param name="arrangement">The party's arrangement at it, or null with no campsite.</param>
    /// <param name="offers">The activities, in order.</param>
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

    /// <summary>The camp's seed (<see cref="CampSeed"/>), which the arrangement was chosen by and the night's talk will be.</summary>
    public uint Seed { get; init; }

    public IReadOnlyList<CampOffer> Offers { get; private set; }

    /// <summary>The rest's hours before anything made adds to them.</summary>
    public int BaseHours { get; }

    /// <summary>The hours the rest will take: the base and what's been made.</summary>
    public int Hours => BaseHours + _added;

    /// <summary>The rows: each activity, then Rest.</summary>
    public IReadOnlyList<CampRow> Rows => [.. Offers.Select(Row), new CampRow(string.Format(CultureInfo.InvariantCulture, RestHoursText, Hours), null, true, null)];

    /// <summary>The cursor's row.</summary>
    public int Cursor { get; private set; }

    /// <summary>Whether the cursor is on Rest, the last row.</summary>
    public bool OnRest => Cursor == Offers.Count;

    /// <summary>The activity open over the rows, or null.</summary>
    public CampOffer? Open { get; private set; }

    /// <summary>Who does the open activity: its best member, or another with the skill the player chose.</summary>
    public Character? Member { get; private set; }

    /// <summary>The recipes the member can make now, in the open activity.</summary>
    public IReadOnlyList<RecipeRecord> Recipes { get; private set; } = [];

    /// <summary>The cursor's recipe in the open activity.</summary>
    public int RecipeCursor { get; private set; }

    /// <summary>The last thing said: what was made, or why nothing was.</summary>
    public string? Message { get; private set; }

    /// <summary>Everything said this camp, in order.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>What's been made this camp, by name, with how many.</summary>
    public IReadOnlyDictionary<string, int> Made => _made;

    /// <summary>How the screen was left, once it was.</summary>
    public CampChoice Choice { get; private set; }

    /// <summary>Whether a talk shows over the screen, in the conversation box, which takes the presses meanwhile; the rows are hidden under it.</summary>
    public bool Talking { get; set; }

    /// <summary>How many talks the debug button has started this camp, which each press's seed counts on (<c>GameSession.TriggerCampTalk</c>).</summary>
    public int TalksTriggered { get; set; }

    /// <summary>The name of a recipe's product, for the rows.</summary>
    public string ProductName(RecipeRecord recipe) => _party.Inventory.Catalog.TryGet(recipe.Product)?.Name ?? recipe.Product.ToString();

    /// <summary>A press on the screen; whether it took it.</summary>
    public bool Press(MenuAction action)
    {
        if (Choice != CampChoice.None || Talking)
            return false;
        return Open is null ? PressRows(action) : PressRecipes(action);
    }

    /// <summary>The cursor onto a row, as the mouse points at one; false if there's no such row or an activity is open.</summary>
    public bool Point(int row)
    {
        if (Open is not null || row < 0 || row >= Offers.Count + 1)
            return false;
        Cursor = row;
        return true;
    }

    /// <summary>The cursor onto a recipe of the open activity, as the mouse points at one; false if there's none.</summary>
    public bool PointRecipe(int row)
    {
        if (Open is null || row < 0 || row >= Recipes.Count)
            return false;
        RecipeCursor = row;
        return true;
    }

    private bool PressRows(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Up or MenuAction.Down:
                Cursor = Math.Clamp(Cursor + action.Vertical(), 0, Offers.Count);
                return true;
            case MenuAction.Accept:
                if (OnRest)
                {
                    Choice = CampChoice.Rest;
                    return true;
                }
                var offer = Offers[Cursor];
                if (!offer.Available)
                {
                    Say(offer.Member is null ? NobodyCan : NothingToMake);
                    return true;
                }
                Open = offer;
                Member = offer.Member;
                Recipes = offer.Recipes;
                RecipeCursor = 0;
                Message = null;
                return true;
            case MenuAction.Back:
                Choice = CampChoice.Leave;
                return true;
            default:
                return false;
        }
    }

    private bool PressRecipes(MenuAction action)
    {
        var open = Open!;
        switch (action)
        {
            case MenuAction.Up or MenuAction.Down:
                if (Recipes.Count > 0)
                    RecipeCursor = Math.Clamp(RecipeCursor + action.Vertical(), 0, Recipes.Count - 1);
                return true;
            case MenuAction.MemberLeft or MenuAction.MemberRight:
                var able = CampActivities.Able(_party, open.Skill).ToList();
                if (able.Count > 1 && Member is { } current)
                {
                    var at = able.IndexOf(current);
                    Member = able[(at + (action == MenuAction.MemberRight ? 1 : able.Count - 1)) % able.Count];
                    RefreshRecipes();
                }
                return true;
            case MenuAction.Accept:
                if (Member is { } member && Recipes.ElementAtOrDefault(RecipeCursor) is { } recipe)
                    Make(open, member, recipe);
                return true;
            case MenuAction.Back:
                Open = null;
                Member = null;
                Recipes = [];
                return true;
            default:
                return false;
        }
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

    /// <summary>The open activity's recipes again, as the ingredients stand now, every offer's with them, the cursor kept within them.</summary>
    private void RefreshRecipes()
    {
        Offers = [.. Offers.Select(o => o with { Recipes = CampActivities.Recipes(_party, o.Member, o.Activity, _recipes) })];
        if (Open is { } open)
        {
            Open = Offers.First(o => o.Activity.Id == open.Activity.Id);
            Recipes = CampActivities.Recipes(_party, Member, open.Activity, _recipes);
            RecipeCursor = Recipes.Count == 0 ? 0 : Math.Min(RecipeCursor, Recipes.Count - 1);
        }
    }

    private void Say(string text)
    {
        Message = text;
        _log.Add(text);
    }

    private CampRow Row(CampOffer offer) => new(offer.Activity.Name, offer, offer.Available,
        offer.Member is null ? NobodyCan
        : offer.Recipes.Count == 0 ? NothingToMake
        : string.Format(CultureInfo.InvariantCulture, MadeNote, offer.Member.Name, offer.Recipes.Count));
}
