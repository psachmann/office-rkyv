using Microsoft.AspNetCore.Builder;
using OfficeRkyv.Api;
using OfficeRkyv.Contracts;

namespace OfficeRkyv.UnitTests.Shared.Auth;

public sealed class CallerAuthenticationTests
{
    [Test]
    public async Task AKeyShorterThanTheMinimum_IsRefusedWhenTheApiIsComposed_NamingTheSetting()
    {
        var builder = WebApplication.CreateBuilder();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");

        var failure = await Assert
            .That(() =>
                builder.AddOfficeRkyvApi(
                    $"Data Source={path}",
                    path + ".blobs",
                    new byte[CallerTokens.MinimumKeyLength - 1]
                )
            )
            .Throws<ArgumentException>();

        await Assert.That(failure!.Message).Contains(CallerTokens.SigningKeySetting);
    }

    [Test]
    public async Task AKeyOfTheMinimumLength_IsAccepted()
    {
        var builder = WebApplication.CreateBuilder();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():n}.db");

        var composed = builder.AddOfficeRkyvApi(
            $"Data Source={path}",
            path + ".blobs",
            new byte[CallerTokens.MinimumKeyLength]
        );

        await Assert.That(composed).IsSameReferenceAs(builder);
    }
}
