using AidynRetold.Core.Ui;

namespace AidynRetold.Core.Tests;

public class HeaderNumbersTests
{
    /// <summary>
    /// The bar splits into Stamina's lighter red and Endurance's darker only once Stamina's points are under half of its full worth, the
    /// user's design (Nov 6, 2026), whatever the hit points; else it's one red.
    /// </summary>
    [Theory]
    [InlineData(30, 30, false)]
    [InlineData(15, 30, false)]
    [InlineData(14, 30, true)]
    [InlineData(19, 40, true)]
    [InlineData(20, 40, false)]
    [InlineData(0, 30, true)]
    [InlineData(0, 0, false)]
    public void TheBarSplitsOnlyWithStaminaUnderHalf(int stamina, int maxStamina, bool splits)
    {
        Assert.Equal(splits, new HeaderNumbers(50, 100, 5, stamina, maxStamina).BarSplits);
        Assert.Equal(splits, new HeaderNumbers(100, 100, 5, stamina, maxStamina).BarSplits);
    }
}
