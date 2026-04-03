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
    public async ValueTask<DariPassphrase?> ShowPasswordPromptAsync()
    {
        var vm = new PasswordPromptViewModel();
        var dialog = new PasswordPromptView { DataContext = vm };

        var tcs = new TaskCompletionSource<bool>();
        vm.Confirmed += () => tcs.TrySetResult(true);
        vm.Cancelled += () => tcs.TrySetResult(false);

        _ = dialog.ShowDialog(_owner).ContinueWith(
            _ => tcs.TrySetResult(false),
            TaskContinuationOptions.OnlyOnRanToCompletion);

        bool confirmed = await tcs.Task.ConfigureAwait(true);

        if (confirmed && !string.IsNullOrEmpty(vm.Passphrase))
            return new DariPassphrase(vm.Passphrase);

        return null;
    }
}
