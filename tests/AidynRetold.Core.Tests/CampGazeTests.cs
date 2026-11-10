using AidynRetold.Core.Camp;
using AidynRetold.Core.Rng;

namespace AidynRetold.Core.Tests;

/// <summary>Where a seated figure looks at camp (the user's design, Nov 9, 2026).</summary>
public sealed class CampGazeTests
{
    private const float Step = 1 / 60f;

    [Fact]
    public void ItBeginsOnTheFireAndHoldsAFocusForTenToSixtySeconds()
    {
        var gaze = new CampGaze(new Lcg(7));
        var seen = new HashSet<int>();
        var held = 0f;
        var changes = 0;
        var last = gaze.Focus;
        Assert.Equal(0, last);
        for (var t = 0f; t < 3000; t += Step)
        {
            gaze.Step(Step, 3);
            held += Step;
            seen.Add(gaze.Focus);
            if (gaze.Focus == last)
                continue;
            // The choice can fall on the same one again, so a held focus can run longer, but never shorter.
            Assert.True(held >= CampGaze.FocusEveryMin - 0.05f, $"held {held}");
            changes++;
            held = 0;
            last = gaze.Focus;
        }
        Assert.True(changes > 20);
        Assert.Equal([0, 1, 2, 3], seen.Order());
    }

    [Fact]
    public void WithNobodyElseItOnlyEverLooksAtTheFire()
    {
        var gaze = new CampGaze(new Lcg(3));
        for (var t = 0f; t < 600; t += Step)
        {
            gaze.Step(Step, 0);
            Assert.Equal(0, gaze.Focus);
        }
    }

    [Fact]
    public void AFigureGoneFromUnderItsFocusLooksBackAtTheFire()
    {
        var gaze = new CampGaze(new Lcg(5));
        for (var t = 0f; t < 600 && gaze.Focus != 3; t += Step)
            gaze.Step(Step, 3);
        Assert.Equal(3, gaze.Focus);
        gaze.Step(Step, 1);
        Assert.Equal(0, gaze.Focus);
    }

    [Fact]
    public void ItTurnsASmallWayEveryFewSecondsAndALargerWayNowAndThenAndComesBack()
    {
        var gaze = new CampGaze(new Lcg(11));
        var (smalls, glances) = (new List<float>(), new List<float>());
        var (lastChange, lastGlance, lastYaw) = (0f, -100f, gaze.Yaw);
        var inGlance = false;
        for (var t = 0f; t < 1200; t += Step)
        {
            gaze.Step(Step, 2);
            var large = MathF.Abs(gaze.Yaw) >= CampGaze.LargeMin - 0.001f;
            Assert.Equal(large, gaze.Glancing);
            if (large && !inGlance)
            {
                glances.Add(t - lastGlance);
                lastGlance = t;
            }
            inGlance = large;
            if (!large && gaze.Yaw != lastYaw)
            {
                smalls.Add(t - lastChange);
                lastChange = t;
            }
            lastYaw = gaze.Yaw;
            Assert.InRange(MathF.Abs(gaze.Yaw), 0, CampGaze.LargeMost + 0.001f);
            Assert.InRange(MathF.Abs(gaze.Nod), 0, CampGaze.LargeNodMost + 0.001f);
        }
        // The first gaps run from the start, and a glance can hide a small turn's moment.
        Assert.All(glances.Skip(1), gap => Assert.InRange(gap, CampGaze.LargeEveryMin - 0.05f, CampGaze.LargeEveryMax + 0.05f));
        Assert.True(glances.Count > 35);
        Assert.Contains(smalls.Skip(1), gap => gap is >= CampGaze.SmallEveryMin - 0.05f and <= CampGaze.SmallEveryMax + 0.05f);
        Assert.True(smalls.Count > 200);
    }
}
