using System.Globalization;
using OfficeRkyv.UI.Shared;

namespace OfficeRkyv.UI.Settings;

/// <summary>What the user chose for how the UI looks and speaks.</summary>
/// <param name="Appearance">Follow the system, or fixed light or dark.</param>
/// <param name="Culture">The UI language.</param>
public sealed record UserSettings(AppearancePreference Appearance, CultureInfo Culture);
