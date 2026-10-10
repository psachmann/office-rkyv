namespace OfficeRkyv.Api.Development;

/// <summary>
/// What the development seed creates: one user, one empty archive owned by that user, and documents in that
/// user's inbox. Data, not UI text, so it is not localized.
/// </summary>
public sealed record DemoData(
    string Email,
    string DisplayName,
    string ArchiveName,
    IReadOnlyList<string> DocumentTitles
)
{
    public static DemoData Default { get; } =
        new(
            Email: "demo@example.com",
            DisplayName: "Demo User",
            ArchiveName: "Taxes 2026",
            DocumentTitles: ["Electricity bill March", "Rental contract", "Insurance policy"]
        );
}
