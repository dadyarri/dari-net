using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Models;
using Dari.App.Services;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for the Settings dialog — currently exposes language selection.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly ILocalizationManager _localization;

    public IReadOnlyList<LanguageItem> AvailableLanguages =>
        _localization.AvailableLanguages;

    [ObservableProperty]
    private LanguageItem _selectedLanguage;

    /// <summary>Raised when the dialog should be closed.</summary>
    public event Action? Closed;

    public SettingsViewModel(IConfigService configService, ILocalizationManager localization)
    {
        _configService = configService;
        _localization = localization;
        _selectedLanguage = AvailableLanguages.FirstOrDefault(
            l => l.Code == localization.CurrentLanguage,
            AvailableLanguages[0]);
    }

    [RelayCommand]
    private void Save()
    {
        _localization.SetLanguage(SelectedLanguage.Code);

        var config = _configService.Load();
        config.Language = SelectedLanguage.Code;
        _configService.Save(config);

        Closed?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();
}
