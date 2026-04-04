using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for the Settings dialog — language and theme selection.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly ILocalizationManager _localization;

    public IReadOnlyList<LanguageItem> AvailableLanguages =>
        _localization.AvailableLanguages;

    public IReadOnlyList<ThemeItem> AvailableThemes { get; }

    [ObservableProperty]
    private LanguageItem _selectedLanguage;

    [ObservableProperty]
    private ThemeItem _selectedTheme;

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
    }

    [RelayCommand]
    private void Save()
    {
        _localization.SetLanguage(SelectedLanguage.Code);

        var config = _configService.Load();
        config.Language = SelectedLanguage.Code;
        config.Theme = SelectedTheme.Code;
        _configService.Save(config);

        ApplyTheme(SelectedTheme.Code);

        Closed?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();

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
