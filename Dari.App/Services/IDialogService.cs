using Dari.Archiver.Crypto;

namespace Dari.App.Services;

/// <summary>
/// Abstracts platform file/UI dialogs for testability.
/// </summary>
public interface IDialogService
{
    /// <summary>Opens a file picker filtered to <c>.dar</c> archives and returns the chosen path, or <see langword="null"/> if cancelled.</summary>
    ValueTask<string?> OpenDarFileAsync();

    /// <summary>Shows a passphrase-entry dialog and returns a <see cref="DariPassphrase"/>, or <see langword="null"/> if the user cancels.</summary>
    ValueTask<DariPassphrase?> ShowPasswordPromptAsync();
}
