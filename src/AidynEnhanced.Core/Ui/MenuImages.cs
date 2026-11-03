using AidynEnhanced.Core.Items;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>
/// UI chrome the menus draw with: images the engine's code names, which need no records and which
/// mods replace as assets under the same IDs (see <c>docs/hardcoded-content.md</c>).
/// </summary>
public static class MenuImages
{
    /// <summary>The Party tab's parchment, with the silhouette, frames and labels painted in.</summary>
    public static readonly RecordId PartyTab = Image(0x00D9);

    /// <summary>The System tab's plain parchment, with nothing painted in.</summary>
    public static readonly RecordId SystemTab = Image(0x00D8);

    /// <summary>
    /// The World tab's parchment, with the world map, the compass and the calendar's days painted in. The three tabs'
    /// images are one scroll: their torn edges run on from one to the next.
    /// </summary>
    public static readonly RecordId WorldTab = Image(0x00DA);

    /// <summary>A pause menu tab's image.</summary>
    public static RecordId Tab(PauseTab tab) => tab switch
    {
        PauseTab.System => SystemTab,
        PauseTab.Party => PartyTab,
        _ => WorldTab,
    };

    /// <summary>
    /// The World tab's widgets: today's square on the calendar (drawn 17 square), Camp and
    /// Journal; and the journal's book and its arrows, up and down.
    /// </summary>
    public static readonly RecordId Today = Image(0x00DC), CampTitle = Image(0x00DD), JournalTitle = Image(0x00E6),
        Book = Image(0x0182), BookUp = Image(0x00D6), BookDown = Image(0x00D5);

    /// <summary>
    /// The System tab's titles: its list's "Menu", Options, Load Game, Save Game, the
    /// name entry's and the Theatre's "Theater" (<c>Theatre.Build</c>).
    /// </summary>
    public static readonly RecordId MenuTitle = Image(0x00F2), OptionsTitle = Image(0x00FD), LoadTitle = Image(0x00F1),
        SaveTitle = Image(0x00FF), EnterNameTitle = Image(0x00F9), TheatreTitle = Image(0x0100);

    /// <summary>The options' volume bar, a braided rope 152 × 18, and the name entry's key cursor, 15 square.</summary>
    public static readonly RecordId VolumeBar = Image(0x00D7), KeyCursor = Image(0x00F8);

    /// <summary>The parchment C-down's details, barter and training draw on, and its shadow.</summary>
    public static readonly RecordId Parchment = Image(0x00E2), ParchmentShadow = Image(0x00E4);

    /// <summary>The hit points bar, cut to the share of hit points left.</summary>
    public static readonly RecordId HealthBar = Image(0x00E0);

    /// <summary>The lists' titles.</summary>
    public static readonly RecordId InventoryTitle = Image(0x00E9), SkillsTitle = Image(0x00EC), SpellsTitle = Image(0x00ED),
        StatsTitle = Image(0x00EE);

    /// <summary>The barter window's titles: the loot's, a shop's stock and the party's list beside a shop.</summary>
    public static readonly RecordId TreasureTitle = Image(0x00F0), BuyTitle = Image(0x00E7), SellTitle = Image(0x00EB);

    /// <summary>The trainer's list's title.</summary>
    public static readonly RecordId TrainTitle = Image(0x00EF);

    /// <summary>The arrows beside a list's title that show it has rows above or below.</summary>
    public static readonly RecordId ScrollUp = Image(0x38D0), ScrollDown = Image(0x38CF);

    /// <summary>
    /// The red arrows that say there's more either way: under the Party tab's knotwork, both ways in trek and left
    /// only in combat; under the System tab's list, right only; and over a save's name.
    /// </summary>
    public static readonly RecordId SliderBoth = Image(0x3756), SliderLeft = Image(0x3755), SliderRight = Image(0x3757);

    /// <summary>
    /// Each equipment slot's image of what it takes, by <see cref="EquipmentSlot"/>, which shows while
    /// the slot is empty (<c>gItemSlotBorg8Indecies</c>).
    /// </summary>
    public static readonly IReadOnlyList<RecordId> EmptySlots =
        [.. new[] { 0x3601, 0x35F8, 0x35FA, 0x35F9, 0x35F3, 0x35F7, 0x35F4, 0x35F5, 0x35F6, 0x35FF, 0x3600, 0x35FB, 0x35FC, 0x35FD, 0x35FE }
            .Select(Image)];

    /// <summary>Lunar's and Solar's icons (<c>getAspectIcon</c>). An item or spell of no aspect has none.</summary>
    public static RecordId? AspectIcon(Aspect aspect) => aspect switch
    {
        Aspect.Lunar => Image(0x01A3),
        Aspect.Solar => Image(0x01A4),
        _ => null,
    };

    /// <summary>Each school's icon (<c>getSchoolIcon</c>). Unschooled has none.</summary>
    public static RecordId? SchoolIcon(MagicSchool school) => school switch
    {
        MagicSchool.Chaos => Image(0x01A9),
        MagicSchool.Elemental => Image(0x01A5),
        MagicSchool.Naming => Image(0x01A6),
        MagicSchool.Necromancy => Image(0x01A7),
        MagicSchool.Star => Image(0x01A8),
        _ => null,
    };

    /// <summary>The flask the original draws for every potion and flask, 16 square (<c>ItemTables.PotionIcon</c>).</summary>
    public static readonly RecordId Potion = Image(0x35E5);

    /// <summary>The scroll the original draws for every scroll, 16 square: every scroll's in the items' icon table (<c>ItemTables.ItemIcons</c>).</summary>
    public static readonly RecordId Scroll = Image(0x35E9);

    /// <summary>
    /// The namespace of the port's own pictures, which no plugin has: the views carry them, and draw the image each stands in for
    /// (<see cref="Shared"/>) in the original look.
    /// </summary>
    public const string OwnNamespace = "aidyn_enhanced";

    /// <summary>
    /// Each potion's and flask's own picture, by its item, the user's art (Nov 2, 2026), where the original draws all of them with
    /// <see cref="Potion"/>: in potion number order from 0x1000, the cut A and J having none.
    /// </summary>
    public static readonly IReadOnlyDictionary<RecordId, RecordId> PotionPictures = new[]
        {
            "fire", "inferno", "sleep_gas", "acid", "healing", "stamina", "curing", "antidote", "restore", "strength", "dexterity", null,
            null, "clarity", "charisma", "defense", "stealth",
        }
        .Select((name, n) => (name, n))
        .Where(p => p.name is not null)
        .ToDictionary(p => RecordId.Hex(RecordTypes.Item, 0x1000 + p.n), p => RecordId.Parse($"{OwnNamespace}:image/potion_{p.name}"));

    /// <summary>
    /// A scroll's picture by its spell's school, the user's art (Nov 2, 2026), where the original draws every scroll with
    /// <see cref="Scroll"/>: its ribbon green for Elemental, purple for Naming, red for Necromancy, gold for Star and white for the
    /// unschooled, which a scroll with no spell has too. No spell is Chaos's, so it has none.
    /// </summary>
    public static readonly IReadOnlyDictionary<MagicSchool, RecordId> ScrollPictures = new[]
        {
            (MagicSchool.Elemental, "elemental"), (MagicSchool.Naming, "naming"), (MagicSchool.Necromancy, "necromancy"),
            (MagicSchool.Star, "star"), (MagicSchool.None, "neutral"),
        }
        .ToDictionary(p => p.Item1, p => RecordId.Parse($"{OwnNamespace}:image/scroll_{p.Item2}"));

    /// <summary>
    /// An item's icon as the menus draw it: a potion's own picture (<see cref="PotionPictures"/>) and a scroll's by its spell's school
    /// (<see cref="ScrollPictures"/>) while its record keeps the shared image, else its record's, so a mod's icon is drawn. The
    /// editor shows the record's.
    /// </summary>
    public static RecordId ItemIcon(Item item, Func<RecordId, SpellRecord?> spells)
    {
        if (item.Icon == Potion && PotionPictures.TryGetValue(item.Id, out var potion))
            return potion;
        if (item.Icon != Scroll || item.Kind != ItemKind.Scroll)
            return item.Icon;
        var school = item.Bonuses.Spell is { } spell && spells(spell.Spell) is { } record ? record.School : MagicSchool.None;
        return ScrollPictures.TryGetValue(school, out var scroll) ? scroll : item.Icon;
    }

    /// <summary>The original's image one of the port's own pictures stands in for, or null for any other image.</summary>
    public static RecordId? Shared(RecordId image) =>
        image.Namespace != OwnNamespace ? null
        : PotionPictures.Values.Contains(image) ? Potion
        : ScrollPictures.Values.Contains(image) ? Scroll
        : null;

    /// <summary>The port's own pictures: the potions' and the scrolls'.</summary>
    public static IEnumerable<RecordId> OwnPictures => PotionPictures.Values.Concat(ScrollPictures.Values);

    private static RecordId Image(int borg8) => RecordId.Hex(RecordTypes.Image, borg8);
}

/// <summary>
/// The silhouette's 15 slots, in the order the original numbers them (<c>DollEquipmentMenu</c>). The
/// helmet's slot reads helmets, per the machine code.
/// </summary>
public enum EquipmentSlot
{
    Weapon,
    Gloves,
    Ring1,
    Helmet,
    Amulet,
    Cloak,
    Armor,
    Belt,
    Boots,
    Shield,
    Wand,
    Ring2,
    Scroll1,
    Scroll2,
    Scroll3,
}
