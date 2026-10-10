using OfficeRkyv.Api.Development;

namespace OfficeRkyv.UnitTests.Development;

public class DemoDataSeedingTests
{
    [Test]
    [Arguments(true, null, true)]
    [Arguments(true, true, true)]
    [Arguments(true, false, false)]
    [Arguments(false, null, false)]
    [Arguments(false, true, false)]
    [Arguments(false, false, false)]
    public async Task SeedsOnlyInDevelopment_AndNotWhenSwitchedOff(bool isDevelopment, bool? setting, bool expected)
    {
        await Assert.That(DemoDataSeeding.IsEnabled(isDevelopment, setting)).IsEqualTo(expected);
    }
}
