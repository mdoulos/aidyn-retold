using AidynRetold.Core.Ui;

namespace AidynRetold.Core.Tests;

public class HeaderNumbersTests
{
    /// <summary>
    /// The bar splits into Stamina's lighter red and Endurance's darker only under three quarters of the maximum with Stamina's points
    /// fewer than the rest's, the user's design (Nov 6, 2026); else it's one red.
    /// </summary>
    [Theory]
    [InlineData(100, 100, 50, false)]
    [InlineData(74, 100, 36, true)]
    [InlineData(74, 100, 37, false)]
    [InlineData(75, 100, 10, false)]
    [InlineData(40, 100, 0, true)]
    [InlineData(0, 100, 0, false)]
    public void TheBarSplitsOnlyHurtWithStaminaBelowEndurance(int hp, int maxHp, int stamina, bool splits) =>
        Assert.Equal(splits, new HeaderNumbers(hp, maxHp, 5, stamina).BarSplits);
}
