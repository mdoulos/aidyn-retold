using AidynEnhanced.Core.Scripts;
using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Tests;

public class DialogueBoxTests
{
    private static readonly RecordId Player = RecordId.Hex(RecordTypes.Entity, 0x023F);
    private static readonly RecordId Trahern = RecordId.Hex(RecordTypes.Entity, 0x0239);
    private static readonly RecordId Brenna = RecordId.Hex(RecordTypes.Entity, 0x0240);

    private static DialogueBox Box() => new(new TextLayout(TextLayoutTests.Font()), 52, 52, Player);

    private static ScriptLine Line(string text, RecordId? speaker) => new(0, speaker, text);

    private static string Lines(int count) => string.Join('\n', Enumerable.Repeat("a", count));

    /// <summary>Choices from one speaker under a line from another.</summary>
    private static ScriptPage Page(string line, params string[] choices) =>
        new(Line(line, Trahern), [.. choices.Select(c => Line(c, Brenna))]);

    [Fact]
    public void ALineSitsAtTheBottomOfTheScreen()
    {
        var layout = Box().Show(new ScriptPage(Line("a", Trahern), []));

        // One line is 11 tall, which would put the box's top at 215: it's never shorter than the border. The band ends at the overscan bar, 228.
        Assert.Equal((180, 228, Trahern), (layout.Top, layout.Bottom, layout.Speaker));
        Assert.Equal(new TextBlock("a", 78, 184, new ScreenRect(78, 184, 294, 230)), layout.Line);
        Assert.Equal((20, 180, 26, 186), (layout.BorderX, layout.BorderY, layout.PortraitX, layout.PortraitY));
        Assert.Empty(layout.Choices);
        Assert.Equal(0, layout.Highlight);
    }

    [Fact]
    public void ABoxGrowsUpwardToFitALongLine()
    {
        // Six lines are 66 tall: 232 - 2 - 66 - 4 = 160.
        var layout = Box().Show(new ScriptPage(Line(Lines(6), Trahern), []));

        Assert.Equal((160, 228), (layout.Top, layout.Bottom));
        Assert.Equal(new TextBlock(Lines(6), 78, 164, new ScreenRect(78, 164, 294, 230)), layout.Line);
        Assert.Equal((20, 160, 26, 166), (layout.BorderX, layout.BorderY, layout.PortraitX, layout.PortraitY));
    }

    [Fact]
    public void ALineTooTallForTheScreenSitsAtTheBase()
    {
        // 22 lines are 242 tall, which wraps in the original's u16s.
        var layout = Box().Show(new ScriptPage(Line(Lines(22), Trahern), []));

        Assert.Equal(180, layout.Top);
        Assert.Equal(new TextBlock(Lines(22), 78, 184, new ScreenRect(78, 184, 294, 230)), layout.Line);
        Assert.Equal(180, layout.BorderY);
    }

    [Fact]
    public void ALineWithNoSpeakerNamesNoOne()
    {
        Assert.Null(Box().Show(new ScriptPage(Line("a", null), [])).Speaker);
    }

    [Fact]
    public void ChoicesFromAnotherSpeakerFillTheBoxUnderTheLine()
    {
        var box = Box();
        var page = Page("a\na\na", "a", "b");

        Assert.False(box.ShowsLineBeforeChoices);
        var layout = box.Show(page);

        // The choices end at 180 - 4 + 52 = 228: two lines of 11 and 4 between them start at 202.
        Assert.Equal(
        [
            new TextBlock("a", 88, 202, new ScreenRect(88, 202, 294, 228)),
            new TextBlock("b", 88, 217, new ScreenRect(88, 202, 294, 228)),
        ], layout.Choices);
        // The line's text ends 2 above them. Three lines are 33 tall: 200 - 33 - 4 = 163.
        Assert.Equal(new TextBlock("a\na\na", 78, 167, new ScreenRect(78, 167, 294, 200)), layout.Line);
        Assert.Equal((163, 228, Trahern), (layout.Top, layout.Bottom, layout.Speaker));
        Assert.Equal((20, 163, 26, 169), (layout.BorderX, layout.BorderY, layout.PortraitX, layout.PortraitY));
    }

    [Fact]
    public void AShortLineAboveChoicesStaysAtTheBaseAndEndsAtTheirTop()
    {
        var layout = Box().Show(Page("a", "a", "b"));

        Assert.Equal(new TextBlock("a", 78, 184, new ScreenRect(78, 184, 294, 200)), layout.Line);
        Assert.Equal(180, layout.Top);
    }

    [Fact]
    public void AChoiceOfSeveralLinesTakesThemAll()
    {
        // The first is two lines: 22 + 4 + 11 = 37, from 228 - 37 = 191.
        var layout = Box().Show(Page("a", "a\na", "b"));

        Assert.Equal([191, 217], layout.Choices.Select(c => c.Y));
        Assert.All(layout.Choices, c => Assert.Equal(new ScreenRect(88, 191, 294, 228), c.Bounds));
    }

    [Fact]
    public void ALineBeforeItsOwnSpeakersChoicesShowsAlone()
    {
        var box = Box();
        var page = new ScriptPage(Line(Lines(6), Trahern), [Line("a", Trahern), Line("b", Trahern)]);

        Assert.False(box.ShowsLineBeforeChoices);
        var alone = box.Show(page);

        Assert.True(box.ShowsLineBeforeChoices);
        Assert.Empty(alone.Choices);
        Assert.Equal(new TextBlock(Lines(6), 78, 164, new ScreenRect(78, 164, 294, 230)), alone.Line);
        Assert.Equal((160, 228, Trahern), (alone.Top, alone.Bottom, alone.Speaker));

        var choices = box.ShowChoices();

        Assert.False(box.ShowsLineBeforeChoices);
        Assert.Null(choices.Line);
        Assert.Equal(Player, choices.Speaker);
        Assert.Equal([202, 217], choices.Choices.Select(c => c.Y));
        // The box keeps its base of four lines over the two choices (the user, in 1.0).
        Assert.Equal(180, choices.Top);
        Assert.Equal(228, choices.Bottom);
        // The portrait and its border stay where the line put them.
        Assert.Equal((20, 160, 26, 166), (choices.BorderX, choices.BorderY, choices.PortraitX, choices.PortraitY));
        Assert.Same(choices, box.Layout);
    }

    [Fact]
    public void ShowingANewPageStartsOverAtTheLine()
    {
        var box = Box();
        box.Show(new ScriptPage(Line(Lines(6), Trahern), [Line("a", Trahern)]));
        Assert.Equal(160, box.ShowChoices().BorderY);

        var layout = box.Show(new ScriptPage(Line("a", Trahern), []));

        Assert.Equal(180, layout.BorderY);
        Assert.NotNull(layout.Line);
        Assert.Equal(Trahern, layout.Speaker);
    }

    [Fact]
    public void ShowChoicesNeedsChoicesWaitingBehindTheLine()
    {
        var box = Box();
        Assert.Throws<InvalidOperationException>(() => box.ShowChoices());

        box.Show(new ScriptPage(Line("a", Trahern), []));
        Assert.Throws<InvalidOperationException>(() => box.ShowChoices());

        box.Show(Page("a", "a"));
        Assert.Throws<InvalidOperationException>(() => box.ShowChoices());

        box.Show(new ScriptPage(Line("a", Trahern), [Line("a", Trahern)]));
        box.ShowChoices();
        Assert.Throws<InvalidOperationException>(() => box.ShowChoices());
    }

    [Fact]
    public void TheCursorStopsAtTheFirstAndLastChoice()
    {
        var box = Box();
        box.Show(Page("a", "a", "b", "c"));

        box.MoveCursor(-1);
        Assert.Equal((0, 0), (box.Highlight, box.Layout!.Highlight));
        box.MoveCursor(1);
        Assert.Equal((1, 1), (box.Highlight, box.Layout!.Highlight));
        box.MoveCursor(5);
        Assert.Equal((2, 2), (box.Highlight, box.Layout!.Highlight));
        box.MoveCursor(-9);
        Assert.Equal((0, 0), (box.Highlight, box.Layout!.Highlight));
    }

    [Fact]
    public void TheCursorDoesNothingWithoutChoices()
    {
        var box = Box();
        box.MoveCursor(1);
        Assert.Equal(0, box.Highlight);

        box.Show(new ScriptPage(Line("a", Trahern), [Line("a", Trahern), Line("b", Trahern)]));
        box.MoveCursor(1);
        Assert.Equal(0, box.Highlight);

        box.ShowChoices();
        box.MoveCursor(1);
        Assert.Equal(1, box.Highlight);
        Assert.Equal(1, box.Layout!.Highlight);
    }

    [Fact]
    public void ANewPageResetsTheCursorAndPulse()
    {
        var box = Box();
        box.Show(Page("a", "a", "b"));
        box.MoveCursor(1);
        StepFrames(box, 5);

        box.Show(Page("a", "a", "b"));

        Assert.Equal(0, box.Highlight);
        Assert.Equal(DialogueBox.ChoiceColor, box.HighlightNow);
    }

    private static void StepFrames(DialogueBox box, int frames)
    {
        for (var i = 0; i < frames * Cadence.FrameTicks; i++)
            box.Step();
    }

    [Fact]
    public void ThePulseAdvancesOnceAFrame()
    {
        var box = Box();
        Assert.Equal(new Rgba(200, 180, 100, 255), box.HighlightNow);

        for (var i = 0; i < Cadence.FrameTicks - 1; i++)
            box.Step();
        Assert.Equal(DialogueBox.ChoiceColor, box.HighlightNow);

        box.Step();
        // Each channel moves by (to - from) / 24 toward zero: -150/24 = -6, -135/24 = -5, -75/24 = -3, -192/24 = -8.
        Assert.Equal(new Rgba(194, 175, 97, 247), box.HighlightNow);
    }

    [Fact]
    public void ThePulseTurnsAroundAfter23FramesAndAgainAtZero()
    {
        var box = Box();

        StepFrames(box, 23);
        Assert.Equal(new Rgba(200 - 6 * 23, 180 - 5 * 23, 100 - 3 * 23, 255 - 8 * 23), box.HighlightNow);

        StepFrames(box, 1);
        Assert.Equal(new Rgba(200 - 6 * 22, 180 - 5 * 22, 100 - 3 * 22, 255 - 8 * 22), box.HighlightNow);

        StepFrames(box, 22);
        Assert.Equal(DialogueBox.ChoiceColor, box.HighlightNow);

        StepFrames(box, 1);
        Assert.Equal(new Rgba(194, 175, 97, 247), box.HighlightNow);
    }

    [Fact]
    public void ThePanelIsInsideTheOverscanBarsAndFeatheredWhereTheyWere()
    {
        var layout = Box().Show(new ScriptPage(Line("a", Trahern), []));

        Assert.Equal(new FeatheredPanel(new ScreenRect(16, 180, 304, 228), 10, 12, 16, 16), layout.Panel);
    }

    [Fact]
    public void OnAWiderOrTallerScreenTheBoxKeepsItsSizeAtTheBottomCentre()
    {
        var box = Box();
        var page = Page("a", "a", "b");
        var small = box.Show(page);

        // A 16:9 screen with twice the original's lines: the frame is at (266, 240).
        box.Resize(852, 480);
        var large = box.Layout!;

        Assert.Equal((266, 240), box.Frame);
        Assert.Equal(small.Panel.Offset(266, 240), large.Panel);
        Assert.Equal((small.BorderX + 266, small.BorderY + 240), (large.BorderX, large.BorderY));
        Assert.Equal(small.Line!.X + 266, large.Line!.X);
        Assert.Equal(small.Line.Bounds.Right - small.Line.Bounds.Left, large.Line.Bounds.Right - large.Line.Bounds.Left);
        Assert.Equal(small.Choices.Select(c => c.Y + 240), large.Choices.Select(c => c.Y));
    }

    // The port's typewriter reveal and voice (TextReveal), off by default. The test font has glyphs for a, b, c and d alone.

    [Fact]
    public void ALineShowsWholeAndSilentByDefault()
    {
        var box = Box();
        box.Show(new ScriptPage(Line("ab cd", Trahern), []));

        Assert.True(box.LineComplete);
        Assert.Equal(4, box.RevealedGlyphs);
        box.Step();
        Assert.Equal(0, box.TakeBlips());
    }

    [Fact]
    public void ALinePrintsAGlyphATickAndItsChoicesWait()
    {
        var box = Box();
        box.GlyphsPerTick = 1;
        box.Show(Page("ab cd", "Yes", "No"));

        Assert.False(box.LineComplete);
        Assert.Equal(0, box.RevealedGlyphs);
        box.Step();
        box.Step();
        Assert.Equal((2, false), (box.RevealedGlyphs, box.LineComplete));
        box.Step();
        box.Step();
        Assert.Equal((4, true), (box.RevealedGlyphs, box.LineComplete));
        box.Step();
        Assert.Equal(4, box.RevealedGlyphs);
    }

    [Fact]
    public void HalfAGlyphATickPrintsEveryOtherTick()
    {
        var box = Box();
        box.GlyphsPerTick = 0.5f;
        box.Show(new ScriptPage(Line("abcd", Trahern), []));

        box.Step();
        Assert.Equal(0, box.RevealedGlyphs);
        box.Step();
        Assert.Equal(1, box.RevealedGlyphs);
        box.Step();
        box.Step();
        Assert.Equal(2, box.RevealedGlyphs);
    }

    [Fact]
    public void CompletingPrintsTheRest()
    {
        var box = Box();
        box.GlyphsPerTick = 1;
        box.Show(new ScriptPage(Line("abcdab", Trahern), []));
        box.Step();

        box.CompleteLine();

        Assert.Equal((6, true), (box.RevealedGlyphs, box.LineComplete));
    }

    [Fact]
    public void TheSameLineShownAgainGoesOnPrinting()
    {
        var box = Box();
        box.GlyphsPerTick = 1;
        var page = new ScriptPage(Line("abcdab", Trahern), []);
        box.Show(page);
        box.Step();
        box.Step();

        box.Show(page);
        Assert.Equal(2, box.RevealedGlyphs);

        // A different line starts over.
        box.Show(new ScriptPage(new ScriptLine(1, Trahern, "abcdab"), []));
        Assert.Equal(0, box.RevealedGlyphs);
    }

    [Fact]
    public void TheVoiceBlipsAtMostEveryThirdTick()
    {
        var box = Box();
        box.GlyphsPerTick = 1;
        box.Voice = true;
        box.Show(new ScriptPage(Line("abcdabcd", Trahern), []));

        var blips = new List<int>();
        for (var i = 0; i < 8; i++)
        {
            box.Step();
            blips.Add(box.TakeBlips());
        }

        // The first glyph blips, then every third: ticks 1, 4 and 7 of 8.
        Assert.Equal([1, 0, 0, 1, 0, 0, 1, 0], blips);
    }

    [Fact]
    public void TwoGlyphsATickBlipOnceATick()
    {
        var box = Box();
        box.GlyphsPerTick = 2;
        box.Voice = true;
        box.Show(new ScriptPage(Line("abcdabcd", Trahern), []));

        box.Step();
        Assert.Equal((2, 1), (box.RevealedGlyphs, box.TakeBlips()));
        box.Step();
        Assert.Equal((4, 0), (box.RevealedGlyphs, box.TakeBlips()));
    }

    [Fact]
    public void TheVoiceOffBlipsNothing()
    {
        var box = Box();
        box.GlyphsPerTick = 1;
        box.Show(new ScriptPage(Line("abcdabcd", Trahern), []));

        for (var i = 0; i < 8; i++)
            box.Step();

        Assert.Equal(0, box.TakeBlips());
    }

    [Theory]
    [InlineData(1.8f, 1f, 1f)]
    [InlineData(0.9f, 2f, 1f)]
    [InlineData(1.0f, 1f, 1.3416408f)]
    [InlineData(0.5f, 1f, 1.5f)]
    [InlineData(4f, 1f, 0.7f)]
    public void ASpeakersPitchFallsWithItsHeight(float height, float scale, float pitch)
    {
        var speaker = new DbRecord(Trahern, "Trahern", Body: new EntityBody(RecordId.Hex(RecordTypes.Actor, 1), 0.5f, scale, height));

        Assert.Equal(pitch, TextReveal.PitchOf(speaker), 1e-5f);
    }

    [Fact]
    public void AMansVoiceIsDeepAndAWomansHighBeforeTheirHeight()
    {
        var body = new EntityBody(RecordId.Hex(RecordTypes.Actor, 1), 0.5f, 1, 1.8f);

        Assert.Equal(0.72f, TextReveal.PitchOf(new DbRecord(Trahern, "Trahern", Body: body, Voice: Voice.Male)), 1e-5f);
        Assert.Equal(1.15f, TextReveal.PitchOf(new DbRecord(Brenna, "Brenna", Body: body, Voice: Voice.Female)), 1e-5f);
        // A short woman is higher still, and a man without a body keeps his base.
        Assert.Equal(1.15f * 1.3416408f, TextReveal.PitchOf(new DbRecord(Brenna, "Brenna", Body: body with { Height = 1 }, Voice: Voice.Female)), 1e-5f);
        Assert.Equal(0.72f, TextReveal.PitchOf(new DbRecord(Trahern, "Trahern", Voice: Voice.Male)), 1e-5f);
        // A guard is as deep as a cyclops, whatever their heights.
        Assert.Equal(0.5f, TextReveal.PitchOf(new DbRecord(Trahern, "Town Guard", Body: body, Voice: Voice.Deep)));
        Assert.Equal(0.5f, TextReveal.PitchOf(new DbRecord(Trahern, "Cyclops", Body: body with { Height = 3.2f }, Voice: Voice.Deep)));
    }

    [Fact]
    public void AModsPitchWinsAndNoBodyIsMiddling()
    {
        var speaker = new DbRecord(Trahern, "Trahern", Body: new EntityBody(RecordId.Hex(RecordTypes.Actor, 1), 0.5f, 1, 4), Voice: Voice.Male, VoicePitch: 1.25f);

        Assert.Equal(1.25f, TextReveal.PitchOf(speaker));
        Assert.Equal(1, TextReveal.PitchOf(new DbRecord(Trahern, "Trahern")));
        Assert.Equal(1, TextReveal.PitchOf(null));
    }

    // Moods (TextReveal.Presets, Guess).

    [Theory]
    [InlineData("Go now!", TextMood.Stern)]
    [InlineData("You DARE?", TextMood.Stern)]
    [InlineData("Will you help us?", TextMood.Pleading)]
    [InlineData("Please, I beg you!", TextMood.Pleading)]
    [InlineData("He is gone...", TextMood.Sad)]
    [InlineData("I am sorry for your loss.", TextMood.Sad)]
    [InlineData("Thank you, friend.", TextMood.Glad)]
    [InlineData("The road runs north to Erromon.", TextMood.Neutral)]
    [InlineData("I will.", TextMood.Neutral)]
    public void AMoodIsGuessedFromTheText(string text, TextMood mood)
    {
        Assert.Equal(mood, TextReveal.Guess(text));
    }

    [Fact]
    public void ATagWinsOverTheGuess()
    {
        Assert.Equal(TextMood.Glad, TextReveal.MoodOf("He is gone...", TextMood.Glad));
        Assert.Equal(TextMood.Sad, TextReveal.MoodOf("He is gone...", null));

        var box = Box();
        box.GlyphsPerTick = 1;
        box.Show(new ScriptPage(new ScriptLine(0, Trahern, "abcd", TextMood.Stern), []));
        Assert.Equal(TextMood.Stern, box.Mood);
    }

    [Fact]
    public void ThePitchBendsThroughTheLine()
    {
        var sad = TextReveal.Presets[TextMood.Sad];
        Assert.Equal(sad.PitchStart, TextReveal.PitchAt(sad, 0));
        Assert.Equal(sad.PitchEnd, TextReveal.PitchAt(sad, 1));
        Assert.Equal((sad.PitchStart + sad.PitchEnd) / 2, TextReveal.PitchAt(sad, 0.5f), 1e-6f);
        Assert.Equal(sad.PitchEnd, TextReveal.PitchAt(sad, 2));
    }

    [Fact]
    public void ThePrintingHoldsAfterACommaAndASentencesEnd()
    {
        // The test font has no comma or full stop, so a word's letters stand for them: none hold.
        var box = Box();
        box.GlyphsPerTick = 1;
        box.Show(new ScriptPage(new ScriptLine(0, Trahern, "ab, cd. ab", TextMood.Neutral), []));
        for (var i = 0; i < 6; i++)
            box.Step();
        Assert.Equal(6, box.RevealedGlyphs);
    }

    [Fact]
    public void AMoodsRateScalesTheSpeed()
    {
        var box = Box();
        box.GlyphsPerTick = 1;
        box.Show(new ScriptPage(new ScriptLine(0, Trahern, "abcdabcdab", TextMood.Sad), []));
        // Sad prints at 0.7 a tick: 7 glyphs in 10 ticks.
        for (var i = 0; i < 10; i++)
            box.Step();
        Assert.Equal(7, box.RevealedGlyphs);
        Assert.Equal(0.7f, box.Progress, 1e-6f);
    }

    /// <summary>A box in a font that has a comma and a full stop too, at glyphs 5 and 7's widths.</summary>
    private static DialogueBox PunctuatedBox() => new(new TextLayout(new FontRecord(
        RecordId.Decimal(RecordTypes.Font, 0), RecordId.Hex(RecordTypes.Image, 0x1C), 4, 2, 8, 9,
        [3, 5, 6, 7, 1, 4, 1, 8],
        new Dictionary<string, int> { [" "] = 0, ["a"] = 1, ["b"] = 2, [","] = 5, ["."] = 7, ["!"] = 6 }, null)), 52, 52, Player);

    [Fact]
    public void ThePrintingHoldsAtPunctuationAsTheMoodHasIt()
    {
        var box = PunctuatedBox();
        box.GlyphsPerTick = 1;
        box.Show(new ScriptPage(new ScriptLine(0, Trahern, "ab, ab. a", TextMood.Neutral), []));
        // a, b, then the comma: 3 glyphs in 3 ticks, then a hold of 3.
        for (var i = 0; i < 3; i++)
            box.Step();
        Assert.Equal(3, box.RevealedGlyphs);
        for (var i = 0; i < 3; i++)
            box.Step();
        Assert.Equal(3, box.RevealedGlyphs);
        box.Step();
        Assert.Equal(4, box.RevealedGlyphs);
        // a, b, then the full stop at glyph 6, then a hold of 8.
        box.Step();
        box.Step();
        Assert.Equal(6, box.RevealedGlyphs);
        for (var i = 0; i < 8; i++)
            box.Step();
        Assert.Equal(6, box.RevealedGlyphs);
        box.Step();
        Assert.Equal((7, true), (box.RevealedGlyphs, box.LineComplete));
    }

    [Fact]
    public void AnEllipsisHoldsOnceAndCompletingSkipsTheHold()
    {
        var box = PunctuatedBox();
        box.GlyphsPerTick = 1;
        box.Show(new ScriptPage(new ScriptLine(0, Trahern, "a... b"), []));
        // Sad prints at 0.7 a tick: a and three dots take 6 ticks, with no hold between the dots, then the sad hold after the last.
        for (var i = 0; i < 6; i++)
            box.Step();
        Assert.Equal((4, TextMood.Sad), (box.RevealedGlyphs, box.Mood));
        box.Step();
        Assert.Equal(4, box.RevealedGlyphs);
        box.CompleteLine();
        Assert.True(box.LineComplete);
    }
}
