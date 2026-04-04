namespace Dari.App.Services;

/// <summary>Represents a selectable UI language.</summary>
public sealed record LanguageItem(string Code, string DisplayName);

/// <summary>
/// Manages the active UI locale by swapping Avalonia resource dictionaries at runtime
/// and exposing a string-lookup API for use in ViewModels.
/// </summary>
public interface ILocalizationManager
{
    /// <summary>Currently active language code (e.g. <c>"en"</c> or <c>"ru"</c>).</summary>
    string CurrentLanguage { get; }

    /// <summary>All languages supported by the application.</summary>
    IReadOnlyList<LanguageItem> AvailableLanguages { get; }

    /// <summary>Returns the localized string for <paramref name="key"/>.</summary>
    string this[string key] { get; }

    /// <summary>
    /// Returns the localized format string for <paramref name="key"/> with
    /// <paramref name="args"/> substituted via <see cref="string.Format(string,object[])"/>.
    /// </summary>
    string Format(string key, params object[] args);

    /// <summary>
    /// Switches the active language to <paramref name="languageCode"/> and reloads
    /// all <c>{DynamicResource}</c> bindings in the active view tree.
    /// </summary>
    void SetLanguage(string languageCode);

    /// <summary>Raised after the active language has changed.</summary>
    event EventHandler? LanguageChanged;
}
