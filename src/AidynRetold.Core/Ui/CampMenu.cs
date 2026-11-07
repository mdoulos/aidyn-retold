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

/// <summary>Where the camp band's cursor is: on the list of tabs down the left, or in the tab's content beside it.</summary>
public enum CampFocus
{
    /// <summary>The tabs: up and down change the tab, right or A enters its content, B breaks camp.</summary>
    Tabs,
    /// <summary>The tab's content: up and down move over its recipes, A makes one or rests, left or B goes back to the tabs.</summary>
    Content,
}

/// <summary>
/// A tick of the camp screen: what its scroll's actor is to play, if anything, and whether the band has just rolled up, so what asked
/// for that, a choice or a talk, is carried out now.
/// </summary>
/// <param name="Cue">A step of the scroll's actor, or null while it holds.</param>
/// <param name="Restart">Whether the actor starts afresh first, in its first state, as a band begins to unroll.</param>
/// <param name="Closed">Whether the band rolled up this tick.</param>
public readonly record struct CampTick(ScrollCue? Cue = null, bool Restart = false, bool Closed = false);

/// <summary>
/// The camp screen, the remade game's (the user's design, Nov 7, 2026): the party round the fire (<see cref="Arrangement"/>) under
/// five tabs, Camp, Food, Potions, Weapons and Armor, on a band of parchment between two rollers, which unrolls and rolls up as the
/// pause menu's scroll does. Camp shows the members and Rest, which says how many hours the night will take; Food, Potions and Armor
/// offer the Cook's, the Alchemist's and the Mechanic's recipes (<see cref="CampActivityRecord"/>), made by the member best at the
/// skill, and each thing made adds its activity's hours to the rest, so the more is made, the later the party wakes. Weapons holds
/// nothing yet. Positions are the view's; the core keeps the state, as it does for the pause menu's tabs.
/// </summary>
/// <remarks>
/// <para>
/// The screen opens with the fire centred and the night's talk in the box over it, if there's one; as the talk ends the picture pans up
/// over a second to its foot (<see cref="Pan"/>), and there the band unrolls (<see cref="Scroll"/>). The triggers, or left and right,
/// change the tab from anywhere. The cursor is on the tabs or in the tab's content (<see cref="Focus"/>), the user's design (Nov 7,
/// 2026): on the tabs, up and down change the tab, and right or A enters the content; in the content, up and down move over a tab's
/// recipes, A makes the cursor's recipe, or on Camp rests, and left goes back to the tabs. C-left and C-right change who makes it
/// among those with the skill; B goes back to the tabs, or on them breaks camp with nothing done; Start opens the pause menu over the
/// camp. A choice, or the
/// debug button's talk, rolls the band up first, and is carried out as it closes (<see cref="Step"/>): a talk then pans the picture back
/// down to the fire, and the band unrolls again as it ends. A controller and a mouse both reach everything: the view points at a tab or a
/// row under the mouse and clicks as A does.
/// </para>
/// </remarks>
public sealed class CampMenu
{
    /// <summary>The camp screen's own lines, the remade game's, as the original has no camp screen.</summary>
    public const string RestText = "Rest", RestHoursText = "Rest ({0} hours)", LeaveText = "Break camp", NobodyCan = "Nobody has the skill.",
        NothingToMake = "Nothing to make.", NothingHere = "", Ready = "{0} is ready.", GoesWrong = "It goes wrong: {0}.", Fails = "{0} fails.",
        MissingIngredients = "You don't have the ingredients.", StackFull = "You can't carry any more of it.", TooLow = "{0} isn't skilled enough.",
        MakerNote = "{0} makes it (C-left, C-right to change)", HoursNote = "+{0} h each", LevelText = "Lv {0}", LocationText = "Current Location: {0}",
        InventoryText = "Total Inventory: {0}";

    /// <summary>The tabs in order, down the left side.</summary>
    public static readonly IReadOnlyList<CampTab> Tabs = [CampTab.Camp, CampTab.Food, CampTab.Potions, CampTab.Weapons, CampTab.Armor];

    /// <summary>How many ticks the pan between the fire centred and the picture's foot takes: a second, the user's design (Nov 7, 2026).</summary>
    public const int PanTicks = 60;

    private readonly Party _party;
    private readonly IReadOnlyList<RecipeRecord> _recipes;
    private readonly IRandom _random;
    private readonly List<string> _log = [];
    private readonly Dictionary<string, int> _made = [];
    private int _added;
    private int _panTicks;

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

    /// <summary>Where the party is camped, for the Camp tab's line: the campsite's name until places have names of their own.</summary>
    public string Location { get; init; } = "";

    public IReadOnlyList<CampOffer> Offers { get; private set; }

    /// <summary>The party's members, in slot order, for the Camp tab's portraits.</summary>
    public IReadOnlyList<Character> Members => [.. _party.Members];

    /// <summary>The rest's hours before anything made adds to them.</summary>
    public int BaseHours { get; }

    /// <summary>The hours the rest will take: the base and what's been made.</summary>
    public int Hours => BaseHours + _added;

    /// <summary>What the Rest button says.</summary>
    public string RestLabel => string.Format(CultureInfo.InvariantCulture, RestHoursText, Hours);

    /// <summary>The Camp tab's line on where the party is.</summary>
    public string LocationLine => string.Format(CultureInfo.InvariantCulture, LocationText, Location);

    /// <summary>The Camp tab's line on the inventory: how many items the party carries, every stack's count, as the Inventory tab's foot says it.</summary>
    public string InventoryLine => string.Format(CultureInfo.InvariantCulture, InventoryText, _party.Inventory.Stacks.Sum(s => s.Quantity));

    /// <summary>A member's level for the row over their portrait.</summary>
    public static string LevelLabel(Character member) => string.Format(CultureInfo.InvariantCulture, LevelText, member.Level);

    /// <summary>
    /// A member's statuses for the Camp tab's badges, the one place they're stacked in a column with their names rather than icons in
    /// a row, the user's design (Nov 7, 2026): hunger's while Hungry or Starving, then tiredness's while Tired or Overtired, each with its
    /// HUD icon and its name.
    /// </summary>
    public static IReadOnlyList<(RecordId Icon, string Name)> Badges(Character member)
    {
        var badges = new List<(RecordId, string)>();
        if (StatusPulse.Rank(member.Hunger) > 0 && HungerDisplay.StatusIcon(member.Hunger) is { } hungry)
            badges.Add((hungry, member.Hunger.ToString()));
        if (RestDisplay.StatusIcon(member.Rest) is { } tired)
            badges.Add((tired, member.Rest.ToString()));
        return badges;
    }

    /// <summary>The tab showing.</summary>
    public CampTab Tab { get; private set; }

    /// <summary>Where the cursor is: on the tabs, or in the tab's content.</summary>
    public CampFocus Focus { get; private set; }

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

    /// <summary>How the screen was left, once it was: chosen on the band, and carried out as it rolls up (<see cref="CampTick.Closed"/>).</summary>
    public CampChoice Choice { get; private set; }

    /// <summary>Whether a talk shows over the screen, in the conversation box, which takes the presses meanwhile; the band is away under it.</summary>
    public bool Talking { get; set; }

    /// <summary>Whether the debug button's talk waits for the band to roll up, to start as it has (<see cref="RequestTalk"/>).</summary>
    public bool TalkPending { get; set; }

    /// <summary>How many talks the debug button has started this camp, which each press's seed counts on (<c>GameSession.TriggerCampTalk</c>).</summary>
    public int TalksTriggered { get; set; }

    /// <summary>
    /// Where a picture taller than the screen is panned to, 0 to 1: 0 with the fire at the screen's centre, for the talk, and 1 with the
    /// picture's foot at the screen's, for the band. It moves a <see cref="PanTicks"/>th a tick toward the fire while a talk shows and
    /// toward the foot otherwise.
    /// </summary>
    public float Pan => (float)_panTicks / PanTicks;

    /// <summary>
    /// The band's scroll while the band is up: unrolling, open or rolling up, as the pause menu's does from its Party tab, rising from
    /// below and dropping away; null while it's away, before the pan has brought the picture's foot up and after it has rolled up.
    /// </summary>
    public PauseScroll? Scroll { get; private set; }

    /// <summary>Whether the band is drawn: up, unrolling, open or rolling.</summary>
    public bool BandShown => Scroll is { ShowsMenu: true };

    /// <summary>Whether the band is open and takes presses: unrolled, no talk showing or coming, and no choice made.</summary>
    public bool Open => Scroll is { Phase: PauseScrollPhase.Open } && !Talking && !TalkPending && Choice == CampChoice.None;

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

    /// <summary>
    /// A tick: the pan toward the fire or the foot, the band unrolling at the foot and stepping while it's up, and rolling up as a choice
    /// or a talk asks. <paramref name="frame"/> and <paramref name="length"/> are the scroll actor's clock and the length of its state, as
    /// <see cref="PauseScroll.Step"/> takes them.
    /// </summary>
    public CampTick Step(int frame, int length)
    {
        if (Scroll is { } scroll)
        {
            if (scroll.Phase == PauseScrollPhase.Open && (Choice != CampChoice.None || TalkPending || Talking))
                return new CampTick(scroll.Close(PauseScroll.Party));
            var cue = scroll.Step(frame, length);
            if (scroll.Phase == PauseScrollPhase.Closed)
            {
                Scroll = null;
                return new CampTick(Closed: true);
            }
            return scroll.StepsScroll ? new CampTick(cue) : default;
        }
        _panTicks = Talking ? Math.Max(0, _panTicks - 1) : Math.Min(PanTicks, _panTicks + 1);
        if (_panTicks < PanTicks || Talking || TalkPending || Choice != CampChoice.None)
            return default;
        // At the foot with nothing pending: the band unrolls, its actor afresh, as the pause menu's is built for each opening, and holds
        // with its rollers just past the Party tab's parchment, as barter's does, rather than running on out to the other tabs' ends.
        Scroll = new PauseScroll(PauseScroll.Party, brightens: false, holds: true, darkens: false);
        return new CampTick(Scroll.Step(frame, length), Restart: true);
    }

    /// <summary>The debug button's talk: the band rolls up first, and the talk starts as it has. False unless the band is open.</summary>
    public bool RequestTalk()
    {
        if (!Open)
            return false;
        TalkPending = true;
        return true;
    }

    /// <summary>A press on the screen; whether it took it. Only the open band takes one.</summary>
    public bool Press(MenuAction action)
    {
        if (!Open)
            return false;
        switch (action)
        {
            case MenuAction.TabPrevious:
                Show(Tabs[(Tabs.ToList().IndexOf(Tab) + Tabs.Count - 1) % Tabs.Count]);
                return true;
            case MenuAction.TabNext:
                Show(Tabs[(Tabs.ToList().IndexOf(Tab) + 1) % Tabs.Count]);
                return true;
            case MenuAction.Left:
                Focus = CampFocus.Tabs;
                return true;
            case MenuAction.Right:
                Focus = CampFocus.Content;
                return true;
            case MenuAction.Up or MenuAction.Down:
                if (Focus == CampFocus.Tabs)
                    Show(Tabs[Math.Clamp(Tabs.ToList().IndexOf(Tab) + action.Vertical(), 0, Tabs.Count - 1)]);
                else if (Recipes.Count > 0)
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
                if (Focus == CampFocus.Tabs)
                {
                    Focus = CampFocus.Content;
                    return true;
                }
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
                if (Focus == CampFocus.Content)
                    Focus = CampFocus.Tabs;
                else
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

    /// <summary>The cursor onto a recipe of the tab showing, as the mouse points at one, the content taking the focus; false if there's none.</summary>
    public bool PointRecipe(int row)
    {
        if (row < 0 || row >= Recipes.Count)
            return false;
        RecipeCursor = row;
        Focus = CampFocus.Content;
        return true;
    }

    /// <summary>The cursor onto the tabs or the content, as the mouse goes over one.</summary>
    public void FocusOn(CampFocus focus) => Focus = focus;

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
