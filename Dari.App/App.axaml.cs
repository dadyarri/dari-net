using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Dari.App.Models;
using Dari.App.Services;
using Dari.App.ViewModels;
using Dari.App.Views;

namespace Dari.App;

public partial class App : Application
{
    public override void Initialize()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var configService = new ConfigService();
        var config = LoadOrInitConfig(configService);

        LocalizationManager.Initialize(config.Language);
        SettingsViewModel.ApplyTheme(config.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            var dialogService = new DialogService(mainWindow);
            mainWindow.DataContext = new MainWindowViewModel(
                dialogService,
                configService,
                LocalizationManager.Current);
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Loads the config from disk. If no config file exists yet (first run), detects the
    /// system locale, writes the default config, and returns it.
    /// </summary>
    private static AppConfig LoadOrInitConfig(IConfigService configService)
    {
        bool firstRun = !File.Exists(ConfigService.ConfigPath);
        AppConfig config = configService.Load();

        if (firstRun)
        {
            config.Language = DetectSystemLanguage();
            configService.Save(config);
        }

        return config;
    }

    private static string DetectSystemLanguage()
    {
        string twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        // Return the detected language only if we have a resource file for it.
        return twoLetter == "ru" ? "ru" : "en";
    }
}
