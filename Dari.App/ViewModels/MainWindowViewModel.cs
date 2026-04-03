using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;
using Dari.Archiver.Archiving;
using Dari.Archiver.Crypto;

namespace Dari.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _title = "Dari";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private ArchiveBrowserViewModel? _browser;

    public MainWindowViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
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
            StatusText = $"Failed to open archive: {ex.Message}";
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
        }

        Browser = new ArchiveBrowserViewModel(reader, passphrase);
        Title = $"Dari — {System.IO.Path.GetFileName(path)}";
        StatusText = $"Opened {reader.Entries.Count} entries.";
    }

    [RelayCommand]
    private async Task CloseArchiveAsync()
    {
        await CloseCurrentBrowserAsync().ConfigureAwait(true);
        Title = "Dari";
        StatusText = "Ready";
    }

    [RelayCommand]
    private void About()
    {
        // Phase G: show About dialog
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
            StatusText = $"Failed to open archive: {ex.Message}";
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
        }

        Browser = new ArchiveBrowserViewModel(reader, passphrase);
        Title = $"Dari — {System.IO.Path.GetFileName(path)}";
        StatusText = $"Opened {reader.Entries.Count} entries.";
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async ValueTask CloseCurrentBrowserAsync()
    {
        if (Browser is { } old)
        {
            Browser = null;
            await old.DisposeAsync().ConfigureAwait(true);
        }
    }
}
