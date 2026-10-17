using System.Security.Cryptography;
using OfficeRkyv.Contracts.Documents;
using OfficeRkyv.Contracts.Users;

namespace OfficeRkyv.UnitTests.Extraction;

/// <summary>
/// The setup of the extraction scenarios: the real pipeline over a real store and blob directory, the OCR
/// programs replaced by <see cref="FakeOcr"/>, and one user who uploads. An upload is answered before its text is
/// extracted, so a test waits for the extraction (<see cref="PipelineHost.WaitForExtractionAsync"/>) before it
/// looks at the result.
/// </summary>
public abstract class ExtractionPipelineTestBase : IAsyncDisposable
{
    protected PipelineHost Pipeline { get; private set; } = null!;

    protected FakeOcr Ocr { get; private set; } = null!;

    protected UserDto Alice { get; private set; } = null!;

    [Before(Test)]
    public async Task SetUpHost()
    {
        Ocr = new FakeOcr();
        Pipeline = await PipelineHost.StartAsync(ocr: Ocr);
        Alice = await CreateUserAsync("alice@example.com");
    }

    protected async Task<UserDto> CreateUserAsync(string email) =>
        (
            await new UsersService.UsersServiceClient(Pipeline.Channel).CreateUserAsync(
                new CreateUserRequest { Email = email, DisplayName = email }
            )
        ).User;

    protected DocumentsService.DocumentsServiceClient DocumentsOf(UserDto user) => new(Pipeline.ChannelFor(user.Id));

    /// <summary>Uploads the content as Alice, the way a client does, and returns the created document.</summary>
    protected Task<DocumentDto> UploadAsync(
        byte[] content,
        string fileName = "file.pdf",
        string? contentType = "application/pdf",
        UserDto? user = null
    ) => TestDocuments.UploadAsync(DocumentsOf(user ?? Alice), fileName, content, contentType);

    protected static string Sha256Of(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    public async ValueTask DisposeAsync() => await Pipeline.DisposeAsync();
}
