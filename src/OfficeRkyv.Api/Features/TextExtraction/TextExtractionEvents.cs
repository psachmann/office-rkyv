using OfficeRkyv.Api.Shared.Extraction;

namespace OfficeRkyv.Api.Features.TextExtraction;

// Payload-only events: Fisher supplies the envelope (stream id, version, timestamp, metadata).

// Recorded once per distinct file content, by the content's hash, and not on any document: the text belongs to
// the content, and an event on a document would move the version its writers hold. The text itself is not in
// the event; it is in the text store beside the blob. The counts are what a later "extract again" needs:
// UnreadPageCount above zero is the set of contents worth reading again once OCR is available.
public sealed record TextExtractionCompleted(
    string Sha256,
    TextExtractionOutcome Outcome,
    int PageCount,
    int OcrPageCount,
    int UnreadPageCount,
    int CharacterCount
);
