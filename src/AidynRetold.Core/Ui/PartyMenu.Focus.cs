using AidynRetold.Core.Characters;
using AidynRetold.Core.Controls;
using AidynRetold.Core.Items;

namespace AidynRetold.Core.Ui;

/// <summary>
/// What the stick is on in the Party tab in the port's look (<see cref="PartyMenu.Focus"/>): the list or grid, the silhouette's slots,
/// the tabs over the list, or the controls under the grid.
/// </summary>
public enum PartyFocus
{
    List,
    Slots,
    Tabs,
    Foot,
}

/// <summary>A button and what it does now, along the Party tab's foot (<see cref="PartyMenu.Prompts"/>).</summary>
/// <param name="Keys">The button's name on the device in use, "Space" or "A", or two joined, "Q/E".</param>
/// <param name="Cancels">Whether it's B's, Cancel, Close, Back or Done, which the cancel's seal presses with the mouse (<see cref="PartyMenu.CancelShows"/>).</param>
public sealed record MenuPrompt(string Keys, string Label, bool Cancels = false);

/// <summary>
/// The stick on the Party tab in the port's look, the user's choice (Nov 3, 2026): it goes to what's that way on the tab, and stops at
/// an edge, where the original's lists wrapped round and its left and right changed the list. And the buttons' prompts along the foot.
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>
    /// The silhouette's corner in the port's layout, centring its slots in the column under the bar; the view draws them from it, and the
    /// stick steps between them by where they are.
    /// </summary>
    public static readonly (int X, int Y) SilhouetteCorner = (22, 86);

    /// <summary>
    /// Each slot's place from the silhouette's corner, by <see cref="EquipmentSlot"/> (<c>DollEquipmentMenu::SetSlotCoords</c>). The
    /// shield's is level with the first scroll's, as its frame painted into <c>0x00D9</c> is and the user's captures show.
    /// </summary>
    public static readonly IReadOnlyList<(int X, int Y)> OriginalSlotPlaces =
    [
        (10, 47), (10, 66), (10, 85), (40, 2), (40, 24), (30, 43), (50, 43), (40, 62),
        (40, 120), (70, 47), (70, 66), (70, 85), (90, 47), (90, 66), (90, 85),
    ];

    /// <summary>
    /// The boots' place in the port's layout, higher than the original's 120 down, so the slots and the figure fit between the lines the
    /// user drew across the silhouette's sheet (Nov 2, 2026), at about y 85 and 218.
    /// </summary>
    public static readonly (int X, int Y) PortBoots = (40, 114);

    /// <summary>How wide and tall a slot of the silhouette is.</summary>
    public const int SlotSize = 16;

    /// <summary>A slot's place from the silhouette's corner: the original's, but the port's boots.</summary>
    public static (int X, int Y) SlotPlace(EquipmentSlot slot, bool originalLook) =>
        !originalLook && slot == EquipmentSlot.Boots ? PortBoots : OriginalSlotPlaces[(int)slot];

    /// <summary>What an empty slot takes, named on its slip.</summary>
    public static string SlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => "Ring",
        EquipmentSlot.Scroll1 or EquipmentSlot.Scroll2 or EquipmentSlot.Scroll3 => "Scroll",
        _ => slot.ToString(),
    };

    private PartyFocus _focus = PartyFocus.List;

    /// <summary>
    /// What the stick is on, in the port's look: the list or grid, a slot of the silhouette (the slot selected, <see cref="SelectedSlot"/>),
    /// the tabs, or the controls under the grid. The grid's first column steps left onto the silhouette's nearest slot and its top line up
    /// onto the tabs; a slot steps to the nearest the stick's way, and right off the silhouette back onto the grid; the tabs step to the
    /// tab beside and down into the list; a list's first row steps up onto the tabs. Nothing wraps round.
    /// </summary>
    public PartyFocus Focus => SelectedSlot is not null ? PartyFocus.Slots : _focus;

    /// <summary>Whether the stick goes by where things are on the tab (<see cref="Focus"/>): the port's look of the tab's own grid.</summary>
    private bool Spatial => Grid && !OriginalLook;

    /// <summary>Puts the stick on a slot of the silhouette, empty or not, as a click on it does.</summary>
    private void FocusSlot(EquipmentSlot slot)
    {
        SelectedSlot = slot;
        Picking = null;
        _focus = PartyFocus.List;
    }

    /// <summary>A step of the stick on the tab in the port's look (<see cref="Focus"/>). False if it isn't the port's look's to take.</summary>
    private bool StepSpatially(int across, int down)
    {
        if (!Spatial || HasPopup)
            return false;
        switch (Focus)
        {
            case PartyFocus.Slots:
                StepSlot(SelectedSlot!.Value, across, down);
                return true;
            case PartyFocus.Tabs:
                if (down > 0)
                    _focus = PartyFocus.List;
                else if (across != 0 && TabOf(List) + across is var tab && tab >= 0 && tab < Tabs.Count)
                {
                    _aim = Tabs[tab];
                    ChangeList(-across);
                }
                return true;
            case PartyFocus.Foot:
                StepFoot(across, down);
                return true;
        }
        if (List == PartyList.Inventory)
        {
            if (GridLayout.Step(Inventory.Cursor, Inventory.Rows.Count, InventoryGrid.Columns, across, down, wrap: false) is { } cell)
                Inventory.Point(cell);
            else if (across < 0)
                FocusSlot(NearestSlot(InventoryGrid.Bounds.Left, GridCursorMiddle(), -1, 0) ?? EquipmentSlot.Weapon);
            else if (down < 0)
                _focus = PartyFocus.Tabs;
            else if (down > 0 && HasFoot)
                _focus = PartyFocus.Foot;
            return true;
        }
        // A list's left and right change the list, as ever; up from its first row goes onto the tabs, and down stops at its last.
        if (across != 0)
            return false;
        var (cursor, count) = List switch
        {
            PartyList.Stats => (Stats.Cursor, Stats.Rows.Count),
            PartyList.Skills => (Skills.Cursor, Skills.Rows.Count),
            _ => (Spells.Cursor, Spells.Rows.Count),
        };
        if (down < 0 && cursor == 0)
            _focus = PartyFocus.Tabs;
        else if (down < 0 || cursor < count - 1)
            MoveList(down);
        return true;
    }

    /// <summary>Moves the cursor of the list showing, which isn't the grid, wrapping round as the original's does.</summary>
    private void MoveList(int by)
    {
        switch (List)
        {
            case PartyList.Inventory: Inventory.Move(by); break;
            case PartyList.Stats: Stats.Move(by); break;
            case PartyList.Skills: Skills.Move(by); break;
            default: Spells.Move(by); break;
        }
    }

    /// <summary>
    /// The mouse's wheel on the tab: it scrolls the grid (<see cref="ScrollGrid"/>), and on a list moves the cursor a row, stopping at its
    /// ends, without ever leaving it for the tabs.
    /// </summary>
    public void Wheel(int by)
    {
        if (List == PartyList.Inventory && Grid)
        {
            ScrollGrid(by);
            return;
        }
        if (Message is not null || DetailsPane.Showing || HasPopup)
        {
            MoveCursor(by);
            return;
        }
        var count = List switch
        {
            PartyList.Inventory => Inventory.Rows.Count,
            PartyList.Stats => Stats.Rows.Count,
            PartyList.Skills => Skills.Rows.Count,
            _ => Spells.Rows.Count,
        };
        if (Cursor + by >= 0 && Cursor + by < count)
            MoveList(by);
    }

    /// <summary>The middle of the grid cursor's cell, down the frame.</summary>
    private float GridCursorMiddle() =>
        InventoryGrid.Bounds.Top + (Inventory.Cursor - Inventory.First) / InventoryGrid.Columns * InventoryGrid.Cell + InventoryGrid.Cell / 2f;

    /// <summary>A slot's middle in the frame, in the port's layout.</summary>
    private static (float X, float Y) SlotMiddle(EquipmentSlot slot)
    {
        var (x, y) = SlotPlace(slot, originalLook: false);
        return (SilhouetteCorner.X + x + SlotSize / 2f, SilhouetteCorner.Y + y + SlotSize / 2f);
    }

    /// <summary>
    /// The slot nearest a point the stick's way from it, where going that way counts half as much as going aside, or null if none is
    /// that way.
    /// </summary>
    private static EquipmentSlot? NearestSlot(float x, float y, int across, int down, EquipmentSlot? from = null)
    {
        EquipmentSlot? best = null;
        var bestScore = float.MaxValue;
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            if (slot == from)
                continue;
            var (sx, sy) = SlotMiddle(slot);
            var (dx, dy) = (sx - x, sy - y);
            var along = across != 0 ? dx * across : dy * down;
            if (along <= 0.5f)
                continue;
            var score = along + 2 * (across != 0 ? Math.Abs(dy) : Math.Abs(dx));
            if (score < bestScore)
                (best, bestScore) = (slot, score);
        }
        return best;
    }

    /// <summary>A step of the stick from a slot: to the nearest that way, right off the silhouette onto the grid, and otherwise nowhere.</summary>
    private void StepSlot(EquipmentSlot from, int across, int down)
    {
        var (x, y) = SlotMiddle(from);
        if (NearestSlot(x, y, across, down, from) is { } next)
            SelectedSlot = next;
        else if (across > 0 && List == PartyList.Inventory)
            SelectedSlot = null;
    }

    /// <summary>
    /// Names a control's first button on the device in use, as the Controls page does, "Space" or "A", which the pause menu sets; null
    /// where it has none. Without it, as in the editor, the tab shows no prompts.
    /// </summary>
    public Func<string, string?>? KeyName { get; set; }

    /// <summary>
    /// The buttons along the tab's foot and what each does now, the user's choice (Nov 3, 2026): A named for what it does with what's
    /// selected, Appraise's Unequip on a worn slot, the details, the member and Back; or a popup's own; or, while items are being chosen
    /// to drop, Select or Unselect and Done. None in the original's look, nor without <see cref="KeyName"/>.
    /// </summary>
    public IReadOnlyList<MenuPrompt> Prompts
    {
        get
        {
            if (OriginalLook || KeyName is null || Shown is null)
                return [];
            var prompts = new List<MenuPrompt>();
            if (Message is not null)
                Add("OK", GameControls.MenuAccept);
            else if (Overlay.Choice is not null)
            {
                Add("Choose", GameControls.MenuAccept);
                Add("Cancel", GameControls.MenuBack);
            }
            else if (DetailsPane.Pinned)
                Add("Close", GameControls.MenuBack);
            else if (DetailsPane.Showing)
            {
            }
            else if (Crafting is not null)
            {
                Add("Choose", GameControls.MenuAccept);
                Add("Back", GameControls.MenuBack);
            }
            else if (Discarding)
            {
                if (Focus == PartyFocus.Foot)
                    FootPrompts(Add);
                else if (BoxRow() is { } row)
                    DiscardHelp(row, Add);
                Add("Done", GameControls.MenuBack);
            }
            else
            {
                switch (Focus)
                {
                    case PartyFocus.Tabs:
                        Add("Open", GameControls.MenuAccept);
                        break;
                    case PartyFocus.Foot:
                        FootPrompts(Add);
                        break;
                    case PartyFocus.Slots:
                        // A on a slot chooses what goes in it (Picking); Appraise takes off what's in it.
                        Add(Worn(SelectedSlot!.Value) is null ? "Choose" : "Change", GameControls.MenuAccept);
                        if (Worn(SelectedSlot!.Value) is not null)
                            Add("Unequip", GameControls.MenuAppraise);
                        if (Details() is not null)
                            Add("Details", GameControls.MenuDetails);
                        break;
                    case PartyFocus.List when BoxRow() is { } row && CursorShows:
                        if (ActionFor(row) is { } named)
                            Add(named.Label(), GameControls.MenuAccept);
                        if (Details() is not null)
                            Add("Details", GameControls.MenuDetails);
                        break;
                    case PartyFocus.List when List == PartyList.Skills && Combat is null && Skills.Current is { } skill && skill.Skill.IsSkill
                        && (Skill)skill.Skill.Slot is Skill.Alchemist or Skill.Mechanic or Skill.Healer:
                        Add("Use", GameControls.MenuAccept);
                        Add("Details", GameControls.MenuDetails);
                        break;
                    case PartyFocus.List when List != PartyList.Inventory && Details() is not null:
                        Add("Details", GameControls.MenuDetails);
                        break;
                }
                if (Party.Slots.Count(s => s is not null) > 1)
                    Add("Member", GameControls.MenuMemberLeft, GameControls.MenuMemberRight);
                Add(Focus == PartyFocus.List && Picking is null ? "Close" : "Back", GameControls.MenuBack);
            }
            return prompts;

            void Add(string label, params string[] controls)
            {
                if (Prompt(label, controls) is { } prompt)
                    prompts.Add(prompt with { Cancels = controls.Contains(GameControls.MenuBack) });
            }
        }
    }

    /// <summary>
    /// Whether the cancel's seal shows, the user's design (Nov 6, 2026): with the mouse in use, wherever B cancels, closes, goes back or
    /// is done, which the prompts say (<see cref="MenuPrompt.Cancels"/>), a red X in wax stands in the top right corner, of a question's
    /// slip or of the frame, and a click on it presses B. A key or a controller shows none, having B.
    /// </summary>
    public bool CancelShows => Pointing && CancelPrompted;

    /// <summary>Whether a prompt is B's now (<see cref="MenuPrompt.Cancels"/>), which a click on the seal's place presses whichever device was last used, the click being the mouse's.</summary>
    public bool CancelPrompted => Prompts.Any(p => p.Cancels);

    /// <summary>
    /// A prompt for a label and the controls that do it, each named on the device in use (<see cref="KeyName"/>), two joined with a
    /// slash; null if any has no button there, or without <see cref="KeyName"/>.
    /// </summary>
    private MenuPrompt? Prompt(string label, params string[] controls)
    {
        if (KeyName is null)
            return null;
        var names = controls.Select(c => KeyName(c)).OfType<string>().ToList();
        return names.Count == controls.Length ? new MenuPrompt(string.Join("/", names), label) : null;
    }

    /// <summary>
    /// Finishes the header's, the silhouette's and the lists' changes at once, in the port's look, so a press made while they move is
    /// taken rather than lost, the user's choice (Nov 3, 2026); the original's waits for them (<c>PartyMenu.CanChange</c>).
    /// </summary>
    private void Settle()
    {
        _memberChange = null;
        _numbersChange = null;
        _slotsChange = null;
        Slide = null;
    }
}
