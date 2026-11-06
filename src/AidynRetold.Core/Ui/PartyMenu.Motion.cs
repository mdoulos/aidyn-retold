using AidynRetold.Core.Characters;
using AidynRetold.Plugins;

namespace AidynRetold.Core.Ui;

/// <summary>
/// The header's numbers as they stood, which a change crossfades from: the hit points, their maximum, the level, and how many of the
/// hit points are Stamina's, which the bar shows in a lighter red at its right end once it splits (<see cref="BarSplits"/>).
/// </summary>
public readonly record struct HeaderNumbers(int Hp, int MaxHp, int Level, int Stamina = 0)
{
    /// <summary>
    /// Whether the bar splits into Stamina's lighter red at its right end and Endurance's darker red, the user's design (Nov 6, 2026):
    /// only once the hit points are under three quarters of their maximum and Stamina's points are fewer than the rest's, Endurance's
    /// (and the Level's under the original's rule). Otherwise the bar is one plain red: a split that showed always jarred, Stamina being
    /// half the bar or more as a rule.
    /// </summary>
    public bool BarSplits => Hp * 4 < MaxHp * 3 && Stamina * 2 < Hp;

    /// <summary>A member's, or none's, all 0.</summary>
    public static HeaderNumbers Of(Character? member) => member is null ? default : new(member.Hp, member.MaxHp, member.Level, member.StaminaPoints);

    /// <summary>The share of the bar these hit points fill, of <paramref name="width"/>.</summary>
    public int Bar(int width) => PartyMenu.HealthBar(Hp, MaxHp, width);

    /// <summary>The share of the bar Stamina's hit points fill, the right end of <see cref="Bar"/>.</summary>
    public int StaminaBar(int width) => PartyMenu.HealthBar(Stamina, MaxHp, width);
}

public sealed partial class PartyMenu
{
    /// <summary>
    /// The frames the header takes to change over to a new member (<c>PartyMenu.PortraitsChange</c>, <c>0x8003b3cc</c>):
    /// the portraits move and resize, and the name, numbers, icons and worn items crossfade. A change to the member shown's
    /// numbers or worn items takes the same (<c>PartyMenu.HealthGoldChange</c>, <c>0x8003be5c</c>;
    /// <c>PartyMenu.EquipmentChange</c>, <c>0x8003e374</c>).
    /// </summary>
    public const int MemberFrames = 15;

    /// <summary>The frames the port's look takes to change the header over, two thirds of the original's, the user's choice (Nov 3, 2026).</summary>
    public const int PortMemberFrames = 10;

    /// <summary>The frames the header takes to change over: the original's in its look, else the port's.</summary>
    private int HeaderFrames => OriginalLook ? MemberFrames : PortMemberFrames;

    /// <summary>
    /// The frames a list takes to slide in as the stick changes it (<c>PartyMenu.ListsSlide</c>, <c>0x80031620</c>), and how far it
    /// slides: the list's width, where the original's slide its 140.
    /// </summary>
    public const int ListFrames = 20, ListDistance = 146, OriginalListDistance = 140;

    /// <summary>The frames the port's look takes to slide a list and its tab in, half the original's, the user's choice (Nov 2, 2026).</summary>
    public const int PortListFrames = ListFrames / 2;

    /// <summary>The frames a list takes to slide in: the original's in its look, else the port's, twice as fast.</summary>
    public int SlideFrames => OriginalLook ? ListFrames : PortListFrames;

    private MenuSlide<(Character Member, IReadOnlyList<int> Order)>? _memberChange;
    private MenuSlide<HeaderNumbers>? _numbersChange;
    private MenuSlide<IReadOnlyList<RecordId?>>? _slotsChange;
    private bool _slotsFromMember;

    /// <summary>The member shown before the last change, while the portraits, name and icons change over to the new one.</summary>
    public Character? Previous => _memberChange?.From.Member;

    /// <summary>The portraits' order before the last change, which they move from.</summary>
    public IReadOnlyList<int> PreviousOrder => _memberChange?.From.Order ?? [];

    /// <summary>How far the portraits, name and icons have changed over to the member shown, eased, from 0 to 1.</summary>
    public float MemberChange => _memberChange?.Change ?? 1;

    /// <summary>
    /// The header's numbers before the last change to them, while they crossfade and the bar tweens to the ones now
    /// (<c>PartyMenu.HealthGoldChange</c>, <c>0x8003be5c</c>): a member change, or anything that changes the member shown,
    /// a potion drunk among them.
    /// </summary>
    public HeaderNumbers? PreviousNumbers => _numbersChange?.From;

    /// <summary>How far the numbers and the bar have changed over, eased, from 0 to 1.</summary>
    public float NumbersChange => _numbersChange?.Change ?? 1;

    /// <summary>
    /// What the silhouette's slots wore before the last change, each slot's icon or null where it wore nothing, while
    /// they crossfade (<c>PartyMenu.EquipmentChange</c>, <c>0x8003e374</c>): a member change, or an item put on or taken off.
    /// </summary>
    public IReadOnlyList<RecordId?>? PreviousSlots => _slotsChange?.From;

    /// <summary>How far the slots have changed over, eased, from 0 to 1.</summary>
    public float SlotsChange => _slotsChange?.Change ?? 1;

    /// <summary>
    /// Whether a slot crossfades from <see cref="PreviousSlots"/> in the change under way. After an item is put on or taken
    /// off, each slot whose item differs, as the original's <c>EquipmentChange</c>. After a member change, only a slot one
    /// member wears something in and the other doesn't, one worn item changing to another at once: the user's choice. A slot is
    /// what it shows (<see cref="SlotImage(EquipmentSlot)"/>), so a flask taken in hand in a battle's pause crossfades into the
    /// weapon's slot, as <see cref="PreviousSlots"/> holds the slots as they showed.
    /// </summary>
    public bool SlotCrossfades(EquipmentSlot slot) => PreviousSlots is { } before && (_slotsFromMember
        ? (before[(int)slot] is null) != (ShownIcon(slot) is null)
        : before[(int)slot] != ShownIcon(slot));

    /// <summary>The list sliding out as another slides in, with the way the lists move (1 right, −1 left), or null.</summary>
    public MenuSlide<PartyList>? Slide { get; private set; }

    /// <summary>How far the lists have slid, eased, from 0 to 1.</summary>
    public float ListChange => Slide?.Change ?? 1;

    /// <summary>
    /// Whether the header, the silhouette or the lists are still changing, which holds off a member change
    /// (<c>PartyMenu.CanChange</c>, <c>0x800304b4</c>, which asks the portraits, the numbers' tweens, the silhouette's and the lists).
    /// </summary>
    public bool Changing => _memberChange is not null || _numbersChange is not null || _slotsChange is not null || Slide is not null;

    /// <summary>The header's numbers as they stand, which a change to the member shown crossfades from.</summary>
    public HeaderNumbers Numbers => HeaderNumbers.Of(Shown);

    /// <summary>What each of the silhouette's slots wears now: its icon, or null.</summary>
    public IReadOnlyList<RecordId?> WornIcons => [.. Enum.GetValues<EquipmentSlot>().Select(ShownIcon)];

    /// <summary>Starts the whole header and the silhouette changing over from the member shown.</summary>
    private void StartMemberChange()
    {
        _memberChange = Shown is { } shown ? new MenuSlide<(Character, IReadOnlyList<int>)>((shown, PortraitOrder), 0, HeaderFrames, MenuTween.Ease) : null;
        ChangeNumbers(Numbers);
        _slotsChange = new MenuSlide<IReadOnlyList<RecordId?>>(WornIcons, 0, HeaderFrames, MenuTween.Ease);
        _slotsFromMember = true;
    }

    /// <summary>
    /// Crossfades the header's numbers from <paramref name="before"/> to the member shown's, and tweens the bar, over 15
    /// frames (<c>PartyMenu.HealthGoldChange</c>, <c>0x8003be5c</c>). The original does it always, even where they're the
    /// same, and finishes a change still under way first, so the new one starts from what's shown.
    /// </summary>
    public void ChangeNumbers(HeaderNumbers before) =>
        _numbersChange = new MenuSlide<HeaderNumbers>(before, 0, HeaderFrames, MenuTween.Ease);

    /// <summary>
    /// The header's numbers and the silhouette crossfading from how they stood, after A did something to the inventory
    /// (<c>PartyMenu.InventoryA</c>, <c>0x80035fa0</c>, which ends with <c>EquipmentChange</c> and <c>HealthGoldChange</c>).
    /// </summary>
    private void ChangeHeader(HeaderNumbers numbers, IReadOnlyList<RecordId?> slots)
    {
        ChangeNumbers(numbers);
        _slotsChange = new MenuSlide<IReadOnlyList<RecordId?>>(slots, 0, HeaderFrames, MenuTween.Ease);
        _slotsFromMember = false;
    }

    /// <summary>Starts the list showing sliding out, the lists moving <paramref name="direction"/>'s way.</summary>
    private void StartListSlide(int direction) => Slide = new MenuSlide<PartyList>(List, Math.Sign(direction), SlideFrames, MenuTween.Ease);

    /// <summary>A frame of the member's, the numbers', the silhouette's and the list's changes.</summary>
    private void StepMotion()
    {
        if (_memberChange?.Step() == false)
            _memberChange = null;
        if (_numbersChange?.Step() == false)
            _numbersChange = null;
        if (_slotsChange?.Step() == false)
            _slotsChange = null;
        if (Slide?.Step() == false)
            Slide = null;
    }
}
