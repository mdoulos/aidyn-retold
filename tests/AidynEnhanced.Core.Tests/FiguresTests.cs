using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Tests;

/// <summary>The figure behind the Party tab's silhouette slots.</summary>
public class FiguresTests
{
    private static DbRecord Member(int portrait, Voice? voice) =>
        new(Entity(0x023F), "Member", Portrait: RecordId.Hex(RecordTypes.Image, portrait), Voice: voice);

    /// <summary>Women are drawn over the woman's figure, Abrecan, Arturo, Baird and Becan over the big man's, and the other men over the man's.</summary>
    [Theory]
    [InlineData(0x0116, Voice.Female, Figure.Woman)] // Brenna
    [InlineData(0x016B, Voice.Female, Figure.Woman)] // Sholeh
    [InlineData(0x0108, Voice.Male, Figure.Man)] // Alaron
    [InlineData(0x012E, Voice.Male, Figure.Man)] // Dougal
    [InlineData(0x0106, Voice.Male, Figure.LargeMan)] // Abrecan
    [InlineData(0x010B, Voice.Male, Figure.LargeMan)] // Arturo
    [InlineData(0x010C, Voice.Male, Figure.LargeMan)] // Baird
    [InlineData(0x0113, Voice.Male, Figure.LargeMan)] // Becan
    [InlineData(0x0144, Voice.Deep, Figure.LargeMan)] // a Town Guard, as a mod might make one a member
    public void EachMembersFigureIsByTheirVoiceAndPortrait(int portrait, Voice voice, Figure figure) =>
        Assert.Equal(figure, Figures.Of(Member(portrait, voice)));

    /// <summary>With no voice or no record to go by, a member is drawn over the man's.</summary>
    [Fact]
    public void AMemberWithNothingToGoByIsTheMans()
    {
        Assert.Equal(Figure.Man, Figures.Of(null));
        Assert.Equal(Figure.Man, Figures.Of(Member(0x0999, null)));
    }
}
