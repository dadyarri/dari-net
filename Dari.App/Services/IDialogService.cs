using Dari.Archiver.Crypto;

namespace Dari.App.Services;

/// <summary>
/// Abstracts platform file/UI dialogs for testability.
/// </summary>
public interface IDialogService
{
    /// <summary>Opens a file picker filtered to <c>.dar</c> archives and returns the chosen path, or <see langword="null"/> if cancelled.</summary>
    ValueTask<string?> OpenDarFileAsync();

    /// <summary>
    /// Shows a passphrase-entry dialog and returns a <see cref="DariPassphrase"/>, or <see langword="null"/> if the user cancels.
    /// </summary>
    /// <param name="validator">
    ///   Optional async callback invoked when the user clicks OK.
    ///   If it returns <see langword="false"/> the dialog stays open and shows an error message.
    /// </param>
    ValueTask<DariPassphrase?> ShowPasswordPromptAsync(
        Func<DariPassphrase, ValueTask<bool>>? validator = null);
}
