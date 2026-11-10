using AidynRetold.Core.Characters;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>The dialog that changes the colour behind a portrait (the user's design, Nov 9, 2026).</summary>
public class PortraitPickerTests
{
    private static readonly RecordId Blue = Image("blue"), Green = Image("green"), Red = Image("red");

    private static readonly PortraitLook Look = new(Image("cutout"), [Blue, Green, Red]);

    private static readonly RecordId Main = Image("main"), Alt = Image("alt");

    private static readonly PortraitLook Dressed = Look with { Overlays = [new PortraitOverlay(Main, "Main Pauldron", Worn: true), new PortraitOverlay(Alt, "Alt Pauldron")] };

    private static RecordId Image(string key) => RecordId.Parse($"acre:image/{key}");

    private static Character Member(PortraitLook? look = null) =>
        Character.Create(Characters.Alaron() with { PortraitLook = look ?? Look }, Characters.Rules, _ => Characters.Fireball);

    [Fact]
    public void ItStartsOnTheFirstColourAndStepsRoundBothWaysChoosingAsItGoes()
    {
        var member = Member();
        var picker = new PortraitPicker(member, Look);
        Assert.Equal(0, picker.Selected);
        Assert.Null(member.PortraitBackground);
        picker.Step(1);
        Assert.Equal(Green, member.PortraitBackground);
        picker.Step(-2);
        Assert.Equal(2, picker.Selected);
        Assert.Equal(Red, member.PortraitBackground);
        picker.Step(1);
        Assert.Equal(Blue, member.PortraitBackground);
    }

    [Fact]
    public void ItOpensOnTheColourChosenAndOneNoLongerOfferedIsTheFirst()
    {
        var member = Member();
        member.PortraitBackground = Red;
        Assert.Equal(2, new PortraitPicker(member, Look).Selected);
        member.PortraitBackground = Image("gone");
        Assert.Equal(0, new PortraitPicker(member, Look).Selected);
    }

    [Fact]
    public void ASwatchIsReachedByTheStepAfterAimingAtIt()
    {
        var member = Member();
        var picker = new PortraitPicker(member, Look);
        picker.AimAt(0);
        picker.Step(1);
        Assert.Equal(Blue, member.PortraitBackground);
        picker.AimAt(2);
        picker.Step(1);
        Assert.Equal(Red, member.PortraitBackground);
    }

    [Fact]
    public void ASaveKeepsTheColour()
    {
        var member = Member();
        Assert.Null(CharacterSaves.Save(member).PortraitBackground);
        new PortraitPicker(member, Look).Step(1);
        Assert.Equal(Green, CharacterSaves.Save(member).PortraitBackground);
    }

    [Fact]
    public void APortraitWearsWhatItWearsToBeginWithUntilAChoiceIsMade()
    {
        var member = Member(Dressed);
        var picker = new PortraitPicker(member, Dressed);
        Assert.True(picker.Wears(0));
        Assert.False(picker.Wears(1));
        Assert.Null(member.PortraitWorn);
        Assert.Equal([Main], Dressed.Worn(null));
    }

    [Fact]
    public void EitherOverlayBothOrNeitherCanBeWornEachOnItsOwn()
    {
        var member = Member(Dressed);
        var picker = new PortraitPicker(member, Dressed);
        picker.AimAtOverlay(1);
        picker.Toggle();
        Assert.Equal([Main, Alt], Dressed.Worn(member.PortraitWorn));
        picker.AimAtOverlay(0);
        picker.Toggle();
        Assert.Equal([Alt], Dressed.Worn(member.PortraitWorn));
        picker.AimAtOverlay(1);
        picker.Toggle();
        Assert.Empty(Dressed.Worn(member.PortraitWorn));
        picker.AimAtOverlay(0);
        picker.Toggle();
        Assert.Equal([Main], Dressed.Worn(member.PortraitWorn));
    }

    [Fact]
    public void WhatsWornIsLaidInTheLooksOrderAndOneNoLongerOfferedIsDropped()
    {
        Assert.Equal([Main, Alt], Dressed.Worn([Alt, Main]));
        Assert.Equal([Alt], Dressed.Worn([Alt, Image("gone")]));
        Assert.Empty(Look.Worn([Alt]));
    }

    [Fact]
    public void ASaveKeepsWhatsWorn()
    {
        var member = Member(Dressed);
        Assert.Null(CharacterSaves.Save(member).PortraitWorn);
        var picker = new PortraitPicker(member, Dressed);
        picker.AimAtOverlay(1);
        picker.Toggle();
        Assert.Equal([Main, Alt], CharacterSaves.Save(member).PortraitWorn);
    }

    [Fact]
    public void TheStickReachesTheOverlaysAndAOrSidewaysPutsThemOnWhileAOnTheColoursCloses()
    {
        var (menu, window, member) = Menu(Member(Dressed));
        Assert.True(menu.OpenPicker(member));
        // Down to the alt pauldron, past the main one, and on with it by A, staying open.
        window.Press(MenuAction.Down);
        window.Press(MenuAction.Down);
        Assert.Equal(2, menu.Picker!.Row);
        window.Accept();
        Assert.NotNull(menu.Picker);
        Assert.Equal([Main, Alt], Dressed.Worn(member.PortraitWorn));
        // Sideways takes it off again and leaves the colour alone.
        window.Press(MenuAction.Right);
        Assert.Equal([Main], Dressed.Worn(member.PortraitWorn));
        Assert.Null(member.PortraitBackground);
        // Round to the colours, where sideways changes the colour and A closes.
        window.Press(MenuAction.Down);
        Assert.False(menu.Picker!.OnOverlay);
        window.Press(MenuAction.Right);
        Assert.Equal(Green, member.PortraitBackground);
        window.Accept();
        Assert.Null(menu.Picker);
    }

    [Fact]
    public void WithNoOverlaysTheStickStaysOnTheColours()
    {
        var picker = new PortraitPicker(Member(), Look);
        picker.MoveRow(1);
        Assert.False(picker.OnOverlay);
    }

    private static (PartyMenu Menu, IPartyWindow Window, Character Member) Menu(Character member)
    {
        var party = TradeData.PartyOf(ItemData.CatalogWith(TradeData.Rules(40), []), null, [member]);
        var menu = new PartyMenu(party, new MenuText([]), _ => null, _ => null);
        return (menu, menu, member);
    }

    [Fact]
    public void TheMenuTakesTheSidewaysPressesWhileItsUpAndAOrBClosesIt()
    {
        var (menu, window, member) = Menu(Member());
        Assert.True(menu.OpenPicker(member));
        Assert.True(menu.HasPopup);
        Assert.False(menu.OpenPicker(member));
        Assert.True(window.Press(MenuAction.Right));
        Assert.Equal(Green, member.PortraitBackground);
        Assert.True(window.Press(MenuAction.Left));
        Assert.Equal(Blue, member.PortraitBackground);
        // B closes the dialog, not the menu; A closes it too.
        Assert.True(window.Back());
        Assert.Null(menu.Picker);
        Assert.True(menu.OpenPicker(member));
        window.Accept();
        Assert.Null(menu.Picker);
        Assert.False(menu.HasPopup);
    }

    [Fact]
    public void AMemberWithoutARecolourablePortraitDoesNotOpenIt()
    {
        var member = Character.Create(Characters.Alaron(), Characters.Rules, _ => Characters.Fireball);
        var (menu, _, _) = Menu(member);
        Assert.False(menu.OpenPicker(member));
        Assert.Null(menu.Picker);
    }
}
