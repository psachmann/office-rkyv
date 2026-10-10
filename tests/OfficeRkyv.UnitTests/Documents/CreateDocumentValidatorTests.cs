using OfficeRkyv.Api.Features.Documents;
using OfficeRkyv.Api.Features.Documents.Handlers;
using OfficeRkyv.Contracts;

namespace OfficeRkyv.UnitTests.Documents;

public sealed class CreateDocumentValidatorTests
{
    private static IEnumerable<(string Property, string Code)> Failures(
        string title = "Invoice",
        string fileName = "invoice.pdf",
        string contentType = "application/pdf"
    ) =>
        new CreateDocumentValidator()
            .Validate(new CreateDocumentCommand(Guid.NewGuid(), title, fileName, contentType, 10, new string('0', 64)))
            .Errors.Select(failure => (failure.PropertyName, failure.ErrorCode));

    [Test]
    public async Task ValidInput_HasNoFailures()
    {
        await Assert.That(Failures()).IsEmpty();
    }

    [Test]
    public async Task ABlankFileName_IsRequired()
    {
        await Assert
            .That(Failures(fileName: "   "))
            .IsEquivalentTo([(nameof(CreateDocumentCommand.FileName), ViolationCodes.Required)]);
    }

    [Test]
    public async Task AFileNameAtTheLimit_Passes_AndOneOverIsTooLong()
    {
        await Assert.That(Failures(fileName: new string('f', CreateDocumentValidator.MaxFileNameLength))).IsEmpty();
        await Assert
            .That(Failures(fileName: new string('f', CreateDocumentValidator.MaxFileNameLength + 1)))
            .IsEquivalentTo([(nameof(CreateDocumentCommand.FileName), ViolationCodes.TooLong)]);
    }

    [Test]
    public async Task AContentTypeOverTheLimit_IsTooLong()
    {
        await Assert
            .That(Failures(contentType: new string('c', CreateDocumentValidator.MaxContentTypeLength + 1)))
            .IsEquivalentTo([(nameof(CreateDocumentCommand.ContentType), ViolationCodes.TooLong)]);
    }

    [Test]
    public async Task EveryBrokenRule_IsReportedAtOnce()
    {
        await Assert
            .That(Failures(title: " ", fileName: ""))
            .IsEquivalentTo([
                (nameof(CreateDocumentCommand.Title), ViolationCodes.Required),
                (nameof(CreateDocumentCommand.FileName), ViolationCodes.Required),
            ]);
    }
}
