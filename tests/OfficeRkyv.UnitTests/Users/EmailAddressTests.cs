using System.Globalization;
using OfficeRkyv.Api.Features.Users;

namespace OfficeRkyv.UnitTests.Users;

public sealed class EmailAddressTests
{
    [Test]
    [Arguments("Alice@Example.COM", "alice@example.com")]
    [Arguments("  alice@example.com ", "alice@example.com")]
    [Arguments("\tBob@Example.com\n", "bob@example.com")]
    [Arguments("alice@example.com", "alice@example.com")]
    public async Task Normalize_TrimsAndLowercases(string input, string expected)
    {
        await Assert.That(EmailAddress.Normalize(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task Normalize_UsesInvariantCasing()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // Turkish culture lowercases 'I' to a dotless 'ı'; the key must not depend on the host culture.
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            await Assert.That(EmailAddress.Normalize("INFO@EXAMPLE.COM")).IsEqualTo("info@example.com");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    [Arguments("alice@example.com")]
    [Arguments("a@b.co")]
    [Arguments("first.last+tag@sub.example.org")]
    public async Task IsWellFormed_AcceptsPlausibleAddresses(string email)
    {
        await Assert.That(EmailAddress.IsWellFormed(email)).IsTrue();
    }

    [Test]
    [Arguments("alice.example.com")] // no @
    [Arguments("alice@@example.com")] // two @
    [Arguments("a@b@example.com")] // two @, apart
    [Arguments("@example.com")] // nothing before @
    [Arguments("alice@")] // nothing after @
    [Arguments("alice@example")] // no dot in the domain
    [Arguments("alice@.example.com")] // domain starts with a dot
    [Arguments("alice@example.com.")] // domain ends with a dot
    [Arguments("ali ce@example.com")] // whitespace inside
    [Arguments("alice@exa mple.com")] // whitespace inside
    public async Task IsWellFormed_RejectsMalformedAddresses(string email)
    {
        await Assert.That(EmailAddress.IsWellFormed(email)).IsFalse();
    }
}
