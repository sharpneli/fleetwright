using System.Globalization;

namespace Fleetwright.Shipgen.Tests;

public class SetupTests
{
    [Fact]
    public void FormattingIsCultureInvariant()
    {
        Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
        Assert.Equal("1234.5", 1234.5.ToString());
    }
}
