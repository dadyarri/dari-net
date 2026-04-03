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
}
