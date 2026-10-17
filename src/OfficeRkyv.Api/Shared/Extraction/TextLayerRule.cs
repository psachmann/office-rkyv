namespace OfficeRkyv.Api.Shared.Extraction;

/// <summary>
/// Decides, for one page of a PDF, whether its text layer is the text of the page or whether the page has to
/// be read by OCR.
/// </summary>
public static class TextLayerRule
{
    /// <summary>
    /// A scan often carries a text layer that holds only a page number or the scanner's stamp, so a layer is
    /// used only when it has this many letters or digits.
    /// </summary>
    public const int MinimumLettersOrDigits = 20;

    /// <summary>True when the page's text layer has at least <see cref="MinimumLettersOrDigits"/> letters or digits.</summary>
    public static bool IsUsable(string? pageText)
    {
        if (pageText is null)
        {
            return false;
        }

        var count = 0;

        foreach (var character in pageText)
        {
            if (char.IsLetterOrDigit(character) && ++count >= MinimumLettersOrDigits)
            {
                return true;
            }
        }

        return false;
    }
}
