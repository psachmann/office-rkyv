using OfficeRkyv.Api.Features.Users;
using OfficeRkyv.Api.Features.Users.Handlers;
using OfficeRkyv.Contracts;

namespace OfficeRkyv.UnitTests.Users;

public sealed class CreateUserCommandValidatorTests
{
    private static IEnumerable<(string Property, string Code)> Failures(string email, string displayName) =>
        new CreateUserCommandValidator()
            .Validate(new CreateUserCommand(email, displayName))
            .Errors.Select(failure => (failure.PropertyName, failure.ErrorCode));

    [Test]
    public async Task ValidInput_HasNoFailures()
    {
        await Assert.That(Failures("alice@example.com", "Alice")).IsEmpty();
    }

    [Test]
    public async Task EmptyAndBlankValues_AreRequired_AndNothingElse()
    {
        // An empty email is only "required": it must not also be reported as malformed.
        await Assert
            .That(Failures("", "   "))
            .IsEquivalentTo([
                (nameof(CreateUserCommand.Email), ViolationCodes.Required),
                (nameof(CreateUserCommand.DisplayName), ViolationCodes.Required),
            ]);
    }

    [Test]
    public async Task AMalformedEmail_IsInvalidFormat()
    {
        await Assert
            .That(Failures("alice.example.com", "Alice"))
            .IsEquivalentTo([(nameof(CreateUserCommand.Email), ViolationCodes.InvalidFormat)]);
    }

    [Test]
    public async Task OverLongValues_AreTooLong_AndEveryBrokenRuleIsReported()
    {
        var email = new string('a', EmailAddress.MaxLength + 1);
        var displayName = new string('n', 100 + 1);

        await Assert
            .That(Failures(email, displayName))
            .IsEquivalentTo([
                (nameof(CreateUserCommand.Email), ViolationCodes.TooLong),
                (nameof(CreateUserCommand.Email), ViolationCodes.InvalidFormat),
                (nameof(CreateUserCommand.DisplayName), ViolationCodes.TooLong),
            ]);
    }

    [Test]
    public async Task RulesSeeTheTrimmedValues()
    {
        await Assert.That(Failures("  alice@example.com  ", "  Alice  ")).IsEmpty();
    }
}
