using Avalonia.Styling;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;

namespace Dari.App.ViewModels;

public sealed record PreviewFontItem(string FamilyName, bool IsMonospace)
{
    public string PreviewSample => "AaBbIiWw 123";
}

/// <summary>ViewModel for the Settings dialog — language and theme selection.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly ILocalizationManager _localization;
    private readonly IReadOnlyList<PreviewFontItem> _allPreviewFonts;
    private readonly IReadOnlyList<PreviewFontItem> _monospacePreviewFonts;

    public IReadOnlyList<LanguageItem> AvailableLanguages =>
        _localization.AvailableLanguages;

    public IReadOnlyList<ThemeItem> AvailableThemes { get; }

    [ObservableProperty]
    private LanguageItem _selectedLanguage;

    [ObservableProperty]
    private ThemeItem _selectedTheme;

    [ObservableProperty]
    private int _previewMaxMb;

    [ObservableProperty]
    private bool _showAllPreviewFonts;

    [ObservableProperty]
    private PreviewFontItem _selectedPreviewFont;

    [ObservableProperty]
    private double _previewFontSize;

    public IReadOnlyList<PreviewFontItem> AvailablePreviewFonts =>
        ShowAllPreviewFonts ? _allPreviewFonts : _monospacePreviewFonts;

    /// <summary>Raised when the dialog should be closed.</summary>
    public event Action? Closed;

    public SettingsViewModel(IConfigService configService, ILocalizationManager localization)
    {
        _configService = configService;
        _localization = localization;

        var config = configService.Load();

        _selectedLanguage = AvailableLanguages.FirstOrDefault(
            l => l.Code == localization.CurrentLanguage,
            AvailableLanguages[0]);

        AvailableThemes =
        [
            new("System", localization["Theme.System"]),
            new("Light",  localization["Theme.Light"]),
            new("Dark",   localization["Theme.Dark"]),
        ];
        _selectedTheme = AvailableThemes.FirstOrDefault(
            t => t.Code == config.Theme,
            AvailableThemes[0]);

        _previewMaxMb = Math.Clamp(config.PreviewMaxMegaBytes, 1, 512);

        var fontItems = BuildPreviewFonts();
        _allPreviewFonts = [new PreviewFontItem("Monospace", true), .. fontItems];
        _monospacePreviewFonts = _allPreviewFonts.Where(f => f.IsMonospace).ToArray();
        _showAllPreviewFonts = false;

        var configuredFont = string.IsNullOrWhiteSpace(config.PreviewMonospaceFontFamily)
            ? "Monospace"
            : config.PreviewMonospaceFontFamily;
        _selectedPreviewFont = _allPreviewFonts.FirstOrDefault(
            f => string.Equals(f.FamilyName, configuredFont, StringComparison.OrdinalIgnoreCase),
            _allPreviewFonts[0]);
        if (!_monospacePreviewFonts.Contains(_selectedPreviewFont))
            _selectedPreviewFont = _monospacePreviewFonts[0];
        _previewFontSize = config.PreviewMonospaceFontSize > 0 ? config.PreviewMonospaceFontSize : 12;
    }

    [RelayCommand]
    private void Save()
    {
        _localization.SetLanguage(SelectedLanguage.Code);

        var config = _configService.Load();
        config.Language = SelectedLanguage.Code;
        config.Theme = SelectedTheme.Code;
        config.PreviewMaxMegaBytes = Math.Clamp(PreviewMaxMb, 1, 512);
        config.PreviewMonospaceFontFamily = SelectedPreviewFont.FamilyName;
        config.PreviewMonospaceFontSize = Math.Clamp(PreviewFontSize, 8, 48);
        _configService.Save(config);

        ApplyTheme(SelectedTheme.Code);

        Closed?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();

    partial void OnShowAllPreviewFontsChanged(bool value)
    {
        OnPropertyChanged(nameof(AvailablePreviewFonts));
        if (!AvailablePreviewFonts.Contains(SelectedPreviewFont))
            SelectedPreviewFont = AvailablePreviewFonts[0];
    }

    private static IReadOnlyList<PreviewFontItem> BuildPreviewFonts()
    {
        var names = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = new List<PreviewFontItem>(names.Length);
        foreach (var name in names)
            items.Add(new PreviewFontItem(name, IsMonospace(name)));
        return items;
    }

    private static bool IsMonospace(string familyName)
    {
        var n = familyName.ToLowerInvariant();
        return n.Contains("mono", StringComparison.Ordinal) ||
               n.Contains("code", StringComparison.Ordinal) ||
               n.Contains("console", StringComparison.Ordinal) ||
               n.Contains("courier", StringComparison.Ordinal) ||
               n.Contains("fixed", StringComparison.Ordinal) ||
               n.Contains("typewriter", StringComparison.Ordinal) ||
               n.Contains("terminal", StringComparison.Ordinal);
    }

    internal static void ApplyTheme(string themeCode)
    {
        if (Avalonia.Application.Current is not { } app) return;
        app.RequestedThemeVariant = themeCode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
