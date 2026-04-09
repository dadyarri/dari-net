using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace Dari.App.Services;

/// <summary>
/// Avalonia implementation of <see cref="ILocalizationManager"/>.
/// Maintains a single <see cref="ResourceInclude"/> entry in
/// <c>Application.Current.Resources.MergedDictionaries</c>; replacing it causes all
/// <c>{DynamicResource}</c> bindings to update automatically.
/// </summary>
public sealed class LocalizationManager : ILocalizationManager
{
    /// <summary>Process-wide singleton set during application startup.</summary>
    public static ILocalizationManager Current { get; private set; } = new LocalizationManager("en");

    private ResourceInclude? _activeInclude;
    private string _currentLanguage;

    /// <inheritdoc/>
    public string CurrentLanguage => _currentLanguage;

    /// <inheritdoc/>
    public IReadOnlyList<LanguageItem> AvailableLanguages { get; } =
    [
        new("en", "English"),
        new("ru", "Русский"),
    ];

    /// <inheritdoc/>
    public event EventHandler? LanguageChanged;

    private LocalizationManager(string initialLanguage)
    {
        _currentLanguage = initialLanguage;
    }

    /// <summary>
    /// Initialises the process-wide singleton with <paramref name="initialLanguage"/> and
    /// loads the corresponding resource dictionary into <c>Application.Current.Resources</c>.
    /// Must be called once from the UI thread after <c>AvaloniaXamlLoader.Load</c>.
    /// </summary>
    public static void Initialize(string initialLanguage)
    {
        var manager = new LocalizationManager(initialLanguage);
        manager.ApplyLanguage(initialLanguage);
        Current = manager;
    }

    /// <inheritdoc/>
    public string this[string key]
    {
        get
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var val) == true
                && val is string s)
            {
                return s;
            }

            return key;
        }
    }

    /// <inheritdoc/>
    public string Format(string key, params object[] args) =>
        string.Format(this[key], args);

    /// <inheritdoc/>
    public void SetLanguage(string languageCode)
    {
        if (languageCode == _currentLanguage) return;
        _currentLanguage = languageCode;
        ApplyLanguage(languageCode);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyLanguage(string languageCode)
    {
        var app = Application.Current ?? throw new InvalidOperationException(
            "LocalizationManager.ApplyLanguage must be called after Avalonia is initialised.");

        var merged = app.Resources.MergedDictionaries;

        if (_activeInclude is not null)
            merged.Remove(_activeInclude);

        var uri = new Uri($"avares://Dari.App/Assets/Locales/{languageCode}.axaml");
        _activeInclude = new ResourceInclude(uri) { Source = uri };
        merged.Add(_activeInclude);
    }
}
