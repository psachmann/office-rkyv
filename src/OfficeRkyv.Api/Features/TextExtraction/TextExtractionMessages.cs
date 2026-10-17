namespace OfficeRkyv.Api.Features.TextExtraction;

/// <summary>
/// Read the text of the file content with this hash. It names the content and not a document, which is what the
/// outcome is keyed by, and it keeps file names and titles out of the message bus's spans.
/// </summary>
public sealed record ExtractText(string Sha256);
