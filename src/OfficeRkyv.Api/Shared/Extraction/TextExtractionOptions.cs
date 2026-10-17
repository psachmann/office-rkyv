namespace OfficeRkyv.Api.Shared.Extraction;

/// <summary>Bound from the <c>TextExtraction</c> configuration section.</summary>
public sealed class TextExtractionOptions
{
    public const string SectionName = "TextExtraction";

    /// <summary>The languages OCR reads, as tesseract spells them: language codes joined with <c>+</c>.</summary>
    public string Languages { get; set; } = "deu+eng";

    /// <summary>The program that reads an image, found on <c>PATH</c> unless this is a path.</summary>
    public string TesseractPath { get; set; } = "tesseract";

    /// <summary>The program that renders a page of a PDF to an image, found on <c>PATH</c> unless this is a path.</summary>
    public string PdftoppmPath { get; set; } = "pdftoppm";

    /// <summary>How long one run of one of the programs may take before it is killed.</summary>
    public TimeSpan TimeLimit { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>The configured languages as separate codes.</summary>
    public IReadOnlyList<string> LanguageCodes =>
        Languages.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
