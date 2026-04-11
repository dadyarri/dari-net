using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;
using Dari.App.ViewModels;
using Dari.Archiver.Archiving;
using Dari.Archiver.Crypto;

namespace Dari.App.ViewModels;

/// <summary>Represents a single entry in the recent-files menu.</summary>
public sealed record RecentFileItem(string Path, string DisplayName);

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly IConfigService _configService;
    private readonly ILocalizationManager _localization;
    private readonly IRecentFilesService _recentFiles;

    /// <summary>Path of the currently open archive; null when no archive is open.</summary>
    private string? _currentArchivePath;

    [ObservableProperty]
    private string _title = "Dari";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private ArchiveBrowserViewModel? _browser;

    /// <summary>Recent files shown in the File → Recent submenu.</summary>
    public ObservableCollection<RecentFileItem> RecentFiles { get; } = [];

    public MainWindowViewModel(
        IDialogService dialogService,
        IConfigService configService,
        ILocalizationManager localization,
        IRecentFilesService recentFiles)
    {
        _dialogService = dialogService;
        _configService = configService;
        _localization = localization;
        _recentFiles = recentFiles;
        StatusText = localization["Status.Ready"];
        RefreshRecentFiles();
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

        var config = _configService.Load();
        Browser = new ArchiveBrowserViewModel(
            reader,
            passphrase,
            _dialogService,
            config.PreviewMaxMegaBytes,
            config.PreviewMonospaceFontFamily,
            config.PreviewMonospaceFontSize);
        _currentArchivePath = path;
        Title = $"Dari — {System.IO.Path.GetFileName(path)}";
        StatusText = _localization.Format("Status.Opened", reader.Entries.Count);
        _recentFiles.Add(path);
        RefreshRecentFiles();
    }

    [RelayCommand]
    private async Task CloseArchiveAsync()
    {
        await CloseCurrentBrowserAsync().ConfigureAwait(true);
        Title = "Dari";
        StatusText = _localization["Status.Ready"];
    }

    [RelayCommand]
    private async Task AboutAsync()
    {
        var vm = new AboutViewModel();
        await _dialogService.ShowAboutAsync(vm).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SettingsAsync()
    {
        var vm = new SettingsViewModel(_configService, _localization, _dialogService);
        await _dialogService.ShowSettingsAsync(vm).ConfigureAwait(true);

        // Sync preview cap in case the user changed it.
        if (Browser is { } browser)
        {
            var config = _configService.Load();
            browser.Preview.MaxPreviewMegaBytes = config.PreviewMaxMegaBytes;
            browser.Preview.MonospaceFontFamily = new FontFamily(
                string.IsNullOrWhiteSpace(config.PreviewMonospaceFontFamily)
                    ? "Monospace"
                    : config.PreviewMonospaceFontFamily);
            browser.Preview.MonospaceFontSize = config.PreviewMonospaceFontSize;
        }
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

        var config = _configService.Load();
        Browser = new ArchiveBrowserViewModel(
            reader,
            passphrase,
            _dialogService,
            config.PreviewMaxMegaBytes,
            config.PreviewMonospaceFontFamily,
            config.PreviewMonospaceFontSize);
        _currentArchivePath = path;
        Title = $"Dari — {System.IO.Path.GetFileName(path)}";
        StatusText = _localization.Format("Status.Opened", reader.Entries.Count);
        _recentFiles.Add(path);
        RefreshRecentFiles();
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
    // Append files (Phase E)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Shows the Append Files dialog for the currently open archive.
    /// If the archive is encrypted, the user is prompted to re-enter the passphrase
    /// (which must match the one used when the archive was opened) before the dialog appears.
    /// After a successful append the archive is reloaded so the new entries are visible.
    /// </summary>
    [RelayCommand]
    private async Task AppendFilesAsync()
    {
        if (Browser is null || _currentArchivePath is null) return;

        // For encrypted archives: require the user to re-enter the passphrase.
        // The validator opens a temporary reader to verify it matches the archive.
        DariPassphrase? passphrase = null;
        if (Browser.Passphrase is not null)
        {
            var archivePath = _currentArchivePath;
            passphrase = await _dialogService.ShowPasswordPromptAsync(async p =>
            {
                using var tempReader = await ArchiveReader.OpenAsync(archivePath).ConfigureAwait(false);
                return await tempReader.VerifyPassphraseAsync(p).ConfigureAwait(false);
            }).ConfigureAwait(true);

            if (passphrase is null) return; // user cancelled
        }

        using var vm = new AppendViewModel(_currentArchivePath, passphrase, _dialogService);
        await _dialogService.ShowAppendDialogAsync(vm).ConfigureAwait(true);
        passphrase?.Dispose();

        if (vm.AppendedCount > 0)
        {
            // Reload the archive so the browser reflects the newly appended entries.
            await OpenArchiveFromPathAsync(_currentArchivePath).ConfigureAwait(true);
            StatusText = _localization.Format("Status.Done.Append", vm.AppendedCount);
        }
    }

    /// <summary>
    /// Called from the main window's drag-and-drop handler when files or folders are dropped
    /// onto an open archive browser. Opens the Append dialog pre-populated with the dropped paths.
    /// </summary>
    public async Task AppendFilesFromPathsAsync(IEnumerable<string> paths)
    {
        if (Browser is null || _currentArchivePath is null) return;

        DariPassphrase? passphrase = null;
        if (Browser.Passphrase is not null)
        {
            var archivePath = _currentArchivePath;
            passphrase = await _dialogService.ShowPasswordPromptAsync(async p =>
            {
                using var tempReader = await ArchiveReader.OpenAsync(archivePath).ConfigureAwait(false);
                return await tempReader.VerifyPassphraseAsync(p).ConfigureAwait(false);
            }).ConfigureAwait(true);

            if (passphrase is null) return;
        }

        using var vm = new AppendViewModel(_currentArchivePath, passphrase, _dialogService);
        vm.AddPaths(paths);
        await _dialogService.ShowAppendDialogAsync(vm).ConfigureAwait(true);
        passphrase?.Dispose();

        if (vm.AppendedCount > 0)
        {
            await OpenArchiveFromPathAsync(_currentArchivePath).ConfigureAwait(true);
            StatusText = _localization.Format("Status.Done.Append", vm.AppendedCount);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async ValueTask CloseCurrentBrowserAsync()
    {
        if (Browser is { } old)
        {
            Browser = null;
            _currentArchivePath = null;
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

    // -----------------------------------------------------------------------
    // Recent files
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task OpenRecentAsync(RecentFileItem? item)
    {
        if (item is null) return;

        if (!File.Exists(item.Path))
        {
            _recentFiles.Remove(item.Path);
            RefreshRecentFiles();
            await _dialogService.ShowMessageAsync(
                _localization["Recent.NotFound.Title"],
                _localization.Format("Recent.NotFound.Message", item.Path)).ConfigureAwait(true);
            return;
        }

        await OpenArchiveFromPathAsync(item.Path).ConfigureAwait(true);
    }

    [RelayCommand]
    private void RemoveRecent(RecentFileItem? item)
    {
        if (item is null) return;
        _recentFiles.Remove(item.Path);
        RefreshRecentFiles();
    }

    [RelayCommand]
    private void ClearRecent()
    {
        _recentFiles.Clear();
        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in _recentFiles.Load())
            RecentFiles.Add(new RecentFileItem(path, System.IO.Path.GetFileName(path)));
    }
}
