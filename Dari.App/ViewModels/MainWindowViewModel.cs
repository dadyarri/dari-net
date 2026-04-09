using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;
using Dari.App.ViewModels;
using Dari.Archiver.Archiving;
using Dari.Archiver.Crypto;

namespace Dari.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly IConfigService _configService;
    private readonly ILocalizationManager _localization;

    [ObservableProperty]
    private string _title = "Dari";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private ArchiveBrowserViewModel? _browser;

    public MainWindowViewModel(
        IDialogService dialogService,
        IConfigService configService,
        ILocalizationManager localization)
    {
        _dialogService = dialogService;
        _configService = configService;
        _localization = localization;
        StatusText = localization["Status.Ready"];
    }

    [RelayCommand]
    private async Task OpenArchiveAsync()
    {
        var path = await _dialogService.OpenDarFileAsync().ConfigureAwait(true);
        if (path is null) return;

        ArchiveReader reader;
        try
        {
            reader = await ArchiveReader.OpenAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = _localization.Format("Status.FailedToOpen", ex.Message);
            return;
        }

        // Close any previously open archive before opening the new one.
        await CloseCurrentBrowserAsync().ConfigureAwait(true);

        // If any entry is encrypted, prompt for the passphrase.
        DariPassphrase? passphrase = null;
        if (reader.Entries.Any(e => e.IsEncrypted))
        {
            passphrase = await _dialogService.ShowPasswordPromptAsync(
                p => reader.VerifyPassphraseAsync(p)).ConfigureAwait(true);
            if (passphrase is null)
            {
                // User cancelled — close the reader and abort.
                await reader.DisposeAsync().ConfigureAwait(true);
                return;
            }

            // Re-open with the passphrase so that ExtractAsync can decrypt entries.
            await reader.DisposeAsync().ConfigureAwait(true);
            reader = await ArchiveReader.OpenAsync(path, passphrase: passphrase).ConfigureAwait(true);
        }

        Browser = new ArchiveBrowserViewModel(reader, passphrase, _dialogService);
        Title = $"Dari — {System.IO.Path.GetFileName(path)}";
        StatusText = _localization.Format("Status.Opened", reader.Entries.Count);
    }

    [RelayCommand]
    private async Task CloseArchiveAsync()
    {
        await CloseCurrentBrowserAsync().ConfigureAwait(true);
        Title = "Dari";
        StatusText = _localization["Status.Ready"];
    }

    [RelayCommand]
    private void About()
    {
        // Phase G: show About dialog
    }

    [RelayCommand]
    private async Task SettingsAsync()
    {
        var vm = new SettingsViewModel(_configService, _localization);
        await _dialogService.ShowSettingsAsync(vm).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NewArchiveAsync()
    {
        using var vm = new CreateArchiveViewModel(_dialogService);
        await _dialogService.ShowCreateArchiveDialogAsync(vm).ConfigureAwait(true);

        if (vm.CreatedArchivePath is { } path)
            await OpenArchiveFromPathAsync(path).ConfigureAwait(true);
    }

    [RelayCommand]
    private void Exit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    // -----------------------------------------------------------------------
    // Open-via-path entry point (used by drag & drop)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Opens the archive at <paramref name="path"/> directly, bypassing the file picker dialog.
    /// Called from the main window's drag-and-drop handler.
    /// </summary>
    public async Task OpenArchiveFromPathAsync(string path)
    {
        ArchiveReader reader;
        try
        {
            reader = await ArchiveReader.OpenAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusText = _localization.Format("Status.FailedToOpen", ex.Message);
            return;
        }

        await CloseCurrentBrowserAsync().ConfigureAwait(true);

        DariPassphrase? passphrase = null;
        if (reader.Entries.Any(e => e.IsEncrypted))
        {
            passphrase = await _dialogService.ShowPasswordPromptAsync(
                p => reader.VerifyPassphraseAsync(p)).ConfigureAwait(true);
            if (passphrase is null)
            {
                await reader.DisposeAsync().ConfigureAwait(true);
                return;
            }

            // Re-open with the passphrase so that ExtractAsync can decrypt entries.
            await reader.DisposeAsync().ConfigureAwait(true);
            reader = await ArchiveReader.OpenAsync(path, passphrase: passphrase).ConfigureAwait(true);
        }

        Browser = new ArchiveBrowserViewModel(reader, passphrase, _dialogService);
        Title = $"Dari — {System.IO.Path.GetFileName(path)}";
        StatusText = _localization.Format("Status.Opened", reader.Entries.Count);
    }

    /// <summary>
    /// Starts the "Create Archive" wizard pre-populated with <paramref name="sourceDirectory"/>.
    /// Called from the main window's drag-and-drop handler when a folder is dropped.
    /// </summary>
    public async Task NewArchiveFromDirectoryAsync(string sourceDirectory)
    {
        using var vm = new CreateArchiveViewModel(_dialogService);
        await vm.SetSourceDirectoryAsync(sourceDirectory).ConfigureAwait(true);
        await _dialogService.ShowCreateArchiveDialogAsync(vm).ConfigureAwait(true);

        if (vm.CreatedArchivePath is { } path)
            await OpenArchiveFromPathAsync(path).ConfigureAwait(true);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async ValueTask CloseCurrentBrowserAsync()
    {
        if (Browser is { } old)
        {
            Browser = null;
            await old.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Called when the main window is closed (Alt+F4 or OS close button).
    /// Cancels any running operation and disposes the archive reader.
    /// </summary>
    public async ValueTask ShutdownAsync()
    {
        await CloseCurrentBrowserAsync().ConfigureAwait(false);
    }
}
