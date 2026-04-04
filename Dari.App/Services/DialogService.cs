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
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Dari Archive",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Dari Archives") { Patterns = ["*.dar"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] },
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
            Title = "Select Destination Folder",
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
        finally
        {
            vm.Completed -= dialog.Close;
        }
        if (!extractTask.IsCompleted)
            await extractTask.ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>?> PickFilesAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Files to Archive",
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
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Dari Archive",
            DefaultExtension = "dar",
            FileTypeChoices =
            [
                new FilePickerFileType("Dari Archives") { Patterns = ["*.dar"] },
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
}
