namespace AidynRetold.Core.Characters;

/// <summary>
/// Whether the aspects, Lunar and Solar, do anything: the user's choice (Nov 5, 2026) is no, with the machinery kept for a day they're
/// wanted back. Off, gear of an aspect fits anyone (<c>Party.CanEquip</c>), a spell's aspect asks nothing of its target
/// (<c>Casting.Requirement</c>) and weighs nothing in resisting it (<c>Casting.Resist</c>), a Lunar caster's chance goes by the part of
/// the day as anyone's (<c>Casting.Chance</c>), the time of day favours no one's blows (<c>Battle.AspectScale</c>) or morale
/// (<c>Battle.AspectOpposed</c>), the AI's casts weigh it not (<c>SpellValues</c>), and the menus draw no aspect icon or line. The
/// records keep their aspects, and the editor's pages no longer offer them.
/// </summary>
public static class Aspects
{
    public static bool Enabled { get; } = false;
}
