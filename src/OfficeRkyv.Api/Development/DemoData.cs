namespace OfficeRkyv.Api.Development;

/// <summary>One demo document: its title, and the sample file (in the <c>Data</c> folder) that is its content.</summary>
public sealed record DemoDocument(string Title, string FileName);

/// <summary>
/// What the development seed creates: one user, one empty archive owned by that user, and documents in that
/// user's inbox. Data, not UI text, so it is not localized.
/// </summary>
public sealed record DemoData(
    string Email,
    string DisplayName,
    string ArchiveName,
    IReadOnlyList<DemoDocument> Documents
)
{
    /// <summary>Where the sample files are: beside the API's binaries, as the project copies them.</summary>
    public static string SampleDirectory => Path.Combine(AppContext.BaseDirectory, "Data");

    public static DemoData Default { get; } =
        new(
            Email: "demo@example.com",
            DisplayName: "Demo User",
            ArchiveName: "Taxes 2026",
            Documents:
            [
                new("Electricity bill March", "sample_1.pdf"),
                new("Rental contract", "sample_2.pdf"),
                new("Insurance policy", "sample_3.pdf"),
            ]
        );
}
