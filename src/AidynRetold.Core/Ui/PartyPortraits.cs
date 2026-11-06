using AidynRetold.Core.Characters;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>A member as the trek HUD's portraits draw one: their portrait, their hunger and tiredness, and how much of their health they have.</summary>
/// <param name="Portrait">The portrait, or null for a member with none.</param>
/// <param name="Health">Hit points over the maximum, 0 to 1.</param>
public sealed record HudMember(RecordId? Portrait, HungerState Hunger = HungerState.Fed, RestState Rest = RestState.Rested, float Health = 1);

/// <summary>
/// The party's portraits at the trek HUD's bottom centre (<c>Portraits::Draw</c>, <c>DrawParty</c>, <c>0x8002583c</c> in
/// 1.0): four glowing frames, each with its member's portrait at half alpha, sliding up from below the screen while
/// they're up and down out of it while they're down. The frames are the original's whatever the plugins put over the image, the
/// user's choice (Nov 5, 2026). The remade game adds, the user's design (Nov 5, 2026), a thin health bar along the bottom of a hurt
/// member's portrait, and in its bottom right corner the icons of a status that pulses over it, hunger's and tiredness's, small and
/// overlapping by half (<see cref="StatusPulse"/>): the Party tab has them at their size.
/// </summary>
public sealed class PartyPortraits
{
    /// <summary>The portrait's frame, the conversation box's too.</summary>
    public static readonly RecordId Frame = HudSprite.Id(0x00DF);

    /// <summary>The ACRE plugin's white pixel, which the bar is drawn with, tinted.</summary>
    public static readonly RecordId Pixel = RecordId.Parse($"{MenuImages.OwnNamespace}:image/hud_pixel");

    /// <summary>The frames' tops when down, out of the screen, and when up; they move 4 a frame between.</summary>
    public const int Down = 240, Up = 184, Step = 4;

    /// <summary>
    /// How far apart the frames stand: the original's four 38 apart; five 32 apart, overlapping by 9.6, so they clear the sundial and
    /// the compass on a 4:3 screen, the portraits 2 apart.
    /// </summary>
    public static readonly int FramePitch = Party.MaxSize > 4 ? 32 : 38;

    /// <summary>
    /// The frames' left edges, one a party slot, centred on the screen's 160 as the original's four were (at 84, 122, 160 and 198):
    /// 75, 107, 139, 171 and 203 for five. Each portrait is 6 inside its frame, both ways.
    /// </summary>
    public static readonly int[] FrameX = [.. Enumerable.Range(0, Party.MaxSize).Select(i => 160 - ((Party.MaxSize - 1) * FramePitch + 42) / 2 + i * FramePitch)];

    public const float FrameScale = 0.8f, PortraitScale = 0.75f;
    public const int PortraitInset = 6;

    /// <summary>The portrait as drawn, 30 square: the original's 40 at three quarters.</summary>
    public const int PortraitSize = 30;

    /// <summary>The health bar: 2 high along the portrait's bottom, 2 in from each side, over a dark backing.</summary>
    public const int BarHeight = 2, BarInset = 2;

    /// <summary>The status icons: 6 square, 1 in from the portrait's right and bottom, each next one half an icon further left.</summary>
    public const int IconSize = 6, IconInset = 1;

    public static readonly Rgba BarBacking = new(0, 0, 0, 160), BarFill = new(96, 208, 96, 255), BarLow = new(224, 48, 40, 255);

    /// <summary>Below a third of the health the bar is red rather than green.</summary>
    public const float LowHealth = 1f / 3;

    /// <summary>Whether they're up, or on their way up.</summary>
    public bool Shown { get; set; } = true;

    /// <summary>The frames' top, which starts out of the screen when the game starts.</summary>
    public int Top { get; private set; } = Down;

    // Frames counted for the pulse over a portrait (StatusPulse), at the HUD's 30 a second.
    private int _pulseFrames;

    /// <summary>A frame: they move 4 toward where they're going (once a rendered frame, whatever its ticks).</summary>
    public void StepFrame()
    {
        _pulseFrames++;
        if (!Shown && Top < Down)
            Top += Step;
        else if (Shown && Top >= Up + 2)
            Top -= Step;
    }

    /// <summary>The icons a member's statuses show on the HUD: hunger's while Hungry or Starving, then tiredness's while Tired or Overtired.</summary>
    public static IReadOnlyList<RecordId> StatusIcons(HungerState hunger, RestState rest)
    {
        var icons = new List<RecordId>();
        if (StatusPulse.Rank(hunger) > 0 && HungerDisplay.StatusIcon(hunger) is { } hungry)
            icons.Add(hungry);
        if (RestDisplay.StatusIcon(rest) is { } tired)
            icons.Add(tired);
        return icons;
    }

    /// <summary>
    /// What they draw: all four frames, then each member's portrait in party order, the screen at
    /// <paramref name="brightness"/>, with the bar and the icons over a portrait that needs them. An empty slot keeps its frame; a dead member looks the same.
    /// </summary>
    /// <param name="members">Each slot's member, or null for an empty slot.</param>
    public IEnumerable<HudSprite> Draw(IReadOnlyList<HudMember?> members, float brightness)
    {
        if (Top >= Down)
            yield break;
        var glow = HudSprite.Times(new Rgba(255, 255, 255, 128), brightness);
        foreach (var x in FrameX)
            yield return new HudSprite(Frame, x, Top, FrameScale, glow) { Original = true };
        // The original never sets the portraits' blue, which keeps the frames' blend and is dimmed again.
        var portrait = glow with { B = ColorBytes.Single(glow.B * brightness) };
        var white = HudSprite.Times(new Rgba(255, 255, 255, 255), brightness);
        for (var i = 0; i < Math.Min(members.Count, FrameX.Length); i++)
        {
            if (members[i] is not { } member)
                continue;
            float left = FrameX[i] + PortraitInset, top = Top + PortraitInset;
            if (member.Portrait is { } image)
            {
                yield return new HudSprite(image, left, top, PortraitScale, portrait);
                if (StatusPulse.Tint(member.Hunger, member.Rest, _pulseFrames / 30f) is { } tint)
                    yield return new HudSprite(image, left, top, PortraitScale, HudSprite.Times(tint, brightness));
            }
            var hurt = member.Health < 1;
            if (hurt)
            {
                var health = Math.Clamp(member.Health, 0, 1);
                var width = PortraitSize - 2 * BarInset;
                var y = top + PortraitSize - BarInset - BarHeight;
                yield return new HudSprite(Pixel, left + BarInset, y, 1, HudSprite.Times(BarBacking, brightness), (width, BarHeight));
                if (health > 0)
                    yield return new HudSprite(Pixel, left + BarInset, y, 1, HudSprite.Times(health < LowHealth ? BarLow : BarFill, brightness), (width * health, BarHeight));
            }
            // The icons sit in the corner, as far from the right as from the bottom, and above the bar while it shows.
            var icons = StatusIcons(member.Hunger, member.Rest);
            var iconBottom = top + PortraitSize - IconInset - (hurt ? BarHeight + 1 : 0);
            for (var n = 0; n < icons.Count; n++)
            {
                var iconLeft = left + PortraitSize - IconInset - IconSize - (icons.Count - 1 - n) * (IconSize / 2f);
                yield return new HudSprite(icons[n], iconLeft, iconBottom - IconSize, 1, white, (IconSize, IconSize));
            }
        }
    }
}
