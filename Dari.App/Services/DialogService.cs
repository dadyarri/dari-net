using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Dari.Archiver.Crypto;
using Dari.App.ViewModels;
using Dari.App.Views;

namespace Dari.App.Services;

/// <summary>
/// Avalonia implementation of <see cref="IDialogService"/>.
/// Constructed with the application's main window so that dialogs are parented correctly.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner) => _owner = owner;

    /// <inheritdoc/>
    public async ValueTask<string?> OpenDarFileAsync()
    {
        var loc = LocalizationManager.Current;
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = loc["Dialog.OpenArchive.Title"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(loc["Dialog.FileType.DariArchives"]) { Patterns = ["*.dar"] },
                new FilePickerFileType(loc["Dialog.FileType.AllFiles"]) { Patterns = ["*.*"] },
            ],
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc/>
    public async ValueTask<DariPassphrase?> ShowPasswordPromptAsync(
        Func<DariPassphrase, ValueTask<bool>>? validator = null)
    {
        var vm = new PasswordPromptViewModel();
        if (validator is not null) vm.SetValidator(validator);
        var dialog = new PasswordPromptView { DataContext = vm };

        await dialog.ShowDialog(_owner).ConfigureAwait(true);

        return vm.IsConfirmed ? vm.VerifiedPassphrase : null;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> PickFolderAsync()
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = LocalizationManager.Current["Dialog.PickFolder.Title"],
            AllowMultiple = false,
        }).ConfigureAwait(true);

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ConflictResolution> ShowNameConflictAsync(string existingPath)
    {
        var vm = new NameConflictViewModel(existingPath);
        var dialog = new NameConflictView { DataContext = vm };
        await dialog.ShowDialog(_owner).ConfigureAwait(true);
        return vm.Resolution;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ShowChecksumErrorAsync(string entryPath, string detail)
    {
        var vm = new ChecksumErrorViewModel(entryPath, detail);
        var dialog = new ChecksumErrorView { DataContext = vm };
        await dialog.ShowDialog(_owner).ConfigureAwait(true);
        return vm.ShouldContinue;
    }

    /// <inheritdoc/>
    public async ValueTask ShowMessageAsync(string title, string message)
    {
        var vm = new MessageViewModel(title, message);
        var dialog = new MessageView { DataContext = vm };
        await dialog.ShowDialog(_owner).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public async ValueTask ShowExtractDialogAsync(ExtractViewModel vm)
    {
        var dialog = new ExtractView { DataContext = vm };
        vm.Completed += dialog.Close;
        var extractTask = vm.StartExtractionAsync();
        try
        {
            await dialog.ShowDialog(_owner).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Owner window closed while dialog was open — fall through to cancellation below.
        }
        finally
        {
            vm.Completed -= dialog.Close;
        }

        if (!extractTask.IsCompleted)
        {
            // Dialog was closed before extraction finished (e.g. owner window force-closed).
            // Cancel the extraction.  Do NOT await with ConfigureAwait(true) here because
            // the Avalonia UI dispatcher may already be shutting down, which causes
            // Dispatcher.Send to throw TaskCanceledException.  Instead, schedule an
            // observation continuation on the thread-pool to suppress
            // UnobservedTaskException without touching the UI dispatcher.
            vm.CancelCommand.Execute(null);
            _ = extractTask.ContinueWith(
                static _ => { },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>?> PickFilesAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationManager.Current["Dialog.PickFiles.Title"],
            AllowMultiple = true,
        }).ConfigureAwait(true);

        if (files.Count == 0) return null;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        return paths.Count == 0 ? null : paths;
    }

    /// <inheritdoc/>
    public async ValueTask<string?> SaveDarFileAsync()
    {
        var loc = LocalizationManager.Current;
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = loc["Dialog.SaveArchive.Title"],
            DefaultExtension = "dar",
            FileTypeChoices =
            [
                new FilePickerFileType(loc["Dialog.FileType.DariArchives"]) { Patterns = ["*.dar"] },
            ],
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc/>
    public async ValueTask ShowCreateArchiveDialogAsync(CreateArchiveViewModel vm)
    {
        var dialog = new CreateArchiveView { DataContext = vm };
        vm.Closed += dialog.Close;
        try
        {
            await dialog.ShowDialog(_owner).ConfigureAwait(true);
        }
        finally
        {
            vm.Closed -= dialog.Close;
        }
    }

    /// <inheritdoc/>
    public async ValueTask ShowSettingsAsync(SettingsViewModel vm)
    {
        var dialog = new SettingsView { DataContext = vm };
        await dialog.ShowDialog(_owner).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public async ValueTask ShowExtractOptionsDialogAsync(ExtractOptionsViewModel vm)
    {
        var dialog = new ExtractOptionsView { DataContext = vm };
        vm.Closed += dialog.Close;
        try
        {
            await dialog.ShowDialog(_owner).ConfigureAwait(true);
        }
        finally
        {
            vm.Closed -= dialog.Close;
        }
    }
}
