using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.Api.Features.TextExtraction;

/// <summary>
/// What became of reading one file content: how many pages it has, how many OCR read and how many could not be
/// read. There is one stream per distinct content, keyed by its SHA-256 the way a user is keyed by their email,
/// so two documents with the same file share one extraction and the store refuses a second one.
/// </summary>
public sealed record TextExtraction : EntityBase
{
    // Lowercase hex SHA-256 of the content: the key of its bytes in the blob store, and of its text beside them.
    // Taken from the event, so replay can never re-key a stream.
    [NaturalKey]
    [JsonInclude]
    public string Sha256 { get; private set; } = string.Empty;

    [JsonInclude]
    public TextExtractionOutcome Outcome { get; private set; }

    [JsonInclude]
    public int PageCount { get; private set; }

    [JsonInclude]
    public int OcrPageCount { get; private set; }

    [JsonInclude]
    public int UnreadPageCount { get; private set; }

    [JsonInclude]
    public int CharacterCount { get; private set; }

    [NaturalKeySource]
    public static TextExtraction Create(IEvent<TextExtractionCompleted> @event) =>
        new()
        {
            Id = @event.StreamId,
            Sha256 = @event.Data.Sha256,
            Outcome = @event.Data.Outcome,
            PageCount = @event.Data.PageCount,
            OcrPageCount = @event.Data.OcrPageCount,
            UnreadPageCount = @event.Data.UnreadPageCount,
            CharacterCount = @event.Data.CharacterCount,
            Status = EntityStatus.Active,
            CreatedAt = @event.Timestamp,
            UpdatedAt = @event.Timestamp,
        };
}
