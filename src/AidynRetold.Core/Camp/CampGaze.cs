using AidynRetold.Core.Rng;

namespace AidynRetold.Core.Camp;

/// <summary>
/// Where a seated figure at camp looks, the user's design (Nov 9, 2026). It looks at a focus: the fire to begin with, and every 10 to 60 seconds the fire or any other figure's
/// place, chosen afresh. Every 3 to 5 seconds its head settles a small way off that focus, and every 20 to 30 seconds it glances a larger way in a random direction for a moment,
/// then back. What this holds is the choosing and the timing; the view turns the head there, as far as a neck can.
/// </summary>
public sealed class CampGaze
{
    public const float FocusEveryMin = 10, FocusEveryMax = 60, SmallEveryMin = 3, SmallEveryMax = 5, LargeEveryMin = 20, LargeEveryMax = 30;

    /// <summary>How far off the focus a small turn goes, across and up or down, in radians.</summary>
    public const float SmallMin = 3 * MathF.PI / 180, SmallMost = 12 * MathF.PI / 180, SmallNodMost = 4 * MathF.PI / 180;

    /// <summary>How far off the focus a larger turn goes, in radians.</summary>
    public const float LargeMin = 30 * MathF.PI / 180, LargeMost = 60 * MathF.PI / 180, LargeNodMost = 9 * MathF.PI / 180;

    /// <summary>How long a larger turn lasts before the head comes back, in seconds.</summary>
    public const float GlanceMin = 1.4f, GlanceMost = 2.4f;

    private readonly IRandom _random;
    private float _focusIn, _smallIn, _largeIn, _glance;
    private float _smallYaw, _smallNod, _largeYaw, _largeNod;

    public CampGaze(IRandom random)
    {
        _random = random;
        _focusIn = Between(FocusEveryMin, FocusEveryMax);
        _smallIn = Between(SmallEveryMin, SmallEveryMax);
        _largeIn = Between(LargeEveryMin, LargeEveryMax);
    }

    /// <summary>What it's focused on: 0 the fire, and from 1 the other figures in the order the view gives them.</summary>
    public int Focus { get; private set; }

    /// <summary>How far across from the focus the head is to turn just now, in radians.</summary>
    public float Yaw => _glance > 0 ? _largeYaw : _smallYaw;

    /// <summary>Whether it's glancing a larger way off its focus just now.</summary>
    public bool Glancing => _glance > 0;

    /// <summary>How far down from level the head is to nod just now, in radians; up is negative.</summary>
    public float Nod => _glance > 0 ? _largeNod : _smallNod;

    /// <param name="seconds">The time gone by.</param>
    /// <param name="others">How many other figures there are to look at.</param>
    public void Step(float seconds, int others)
    {
        _focusIn -= seconds;
        if (_focusIn <= 0)
        {
            Focus = (int)_random.Below((uint)others + 1);
            _focusIn = Between(FocusEveryMin, FocusEveryMax);
        }
        else if (Focus > others)
        {
            Focus = 0;
        }
        _smallIn -= seconds;
        if (_smallIn <= 0)
        {
            _smallYaw = Signed(SmallMin, SmallMost);
            _smallNod = Signed(0, SmallNodMost);
            _smallIn = Between(SmallEveryMin, SmallEveryMax);
        }
        _glance -= seconds;
        _largeIn -= seconds;
        if (_largeIn <= 0)
        {
            _largeYaw = Signed(LargeMin, LargeMost);
            _largeNod = Signed(0, LargeNodMost);
            _glance = Between(GlanceMin, GlanceMost);
            _largeIn = Between(LargeEveryMin, LargeEveryMax);
        }
    }

    private float Between(float least, float most) => least + _random.Below(1001) / 1000f * (most - least);

    private float Signed(float least, float most) => Between(least, most) * (_random.Below(2) == 0 ? -1 : 1);
}
