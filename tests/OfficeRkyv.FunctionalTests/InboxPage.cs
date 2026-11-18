using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace OfficeRkyv.FunctionalTests;

/// <summary>
/// The inbox page as a person uses it: the documents that are in no archive, and the action that brings new ones in.
/// Only this page offers "New document"; a new document always lands here.
/// </summary>
public sealed class InboxPage(IPage page) : DocumentListPage(page)
{
    /// <summary>One chosen file: its name, its bytes and the type the browser would declare for it.</summary>
    public sealed record UploadFile(string Name, byte[] Content, string MimeType = "application/pdf");

    public ILocator NewDocumentButton => page.GetByRole(AriaRole.Button, new() { Name = "New document" });

    /// <summary>The files of the open upload dialog, each with its state.</summary>
    public ILocator UploadItems => Dialog.GetByTestId("upload-item");

    /// <summary>The state text of one file in the open upload dialog, found by the file's name.</summary>
    public ILocator UploadState(string fileName) =>
        UploadItems.Filter(new() { HasText = fileName }).GetByTestId("upload-state");

    /// <summary>
    /// Opens the new-document action, chooses the files and waits until each has been uploaded or refused.
    /// The dialog stays open, so a test can look at what each file's state says.
    /// </summary>
    public async Task UploadAsync(params UploadFile[] files)
    {
        await NewDocumentButton.ClickAsync();
        await Dialog
            .GetByLabel("Choose files")
            .SetInputFilesAsync(
                files
                    .Select(file => new FilePayload
                    {
                        Name = file.Name,
                        MimeType = file.MimeType,
                        Buffer = file.Content,
                    })
                    .ToArray()
            );

        // The dialog cannot be closed while a file is still going up.
        await Expect(CloseUploadButton).ToBeEnabledAsync();
    }

    private ILocator CloseUploadButton => Dialog.Locator("button").Filter(new() { HasTextString = "Close" });

    public async Task CloseUploadDialogAsync()
    {
        await CloseUploadButton.ClickAsync();
        await Expect(Dialog).ToBeHiddenAsync();
    }

    /// <summary>
    /// Gives the inbox a document with this title: a small file of that name is uploaded, and the server titles it
    /// from the file name.
    /// </summary>
    public async Task CreateDocumentAsync(string title)
    {
        await UploadAsync(new UploadFile($"{title}.pdf", System.Text.Encoding.UTF8.GetBytes($"%PDF-1.4 {title}")));
        await CloseUploadDialogAsync();
    }
}
