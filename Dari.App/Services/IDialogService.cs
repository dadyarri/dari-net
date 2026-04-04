using Dari.Archiver.Crypto;
using Dari.App.ViewModels;

namespace Dari.App.Services;

/// <summary>Resolution chosen when a target file already exists during extraction.</summary>
public enum ConflictResolution { Overwrite, Skip, Rename }

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

    /// <summary>Opens a folder picker and returns the chosen directory path, or <see langword="null"/> if cancelled.</summary>
    ValueTask<string?> PickFolderAsync();

    /// <summary>
    /// Shows a name-conflict dialog for <paramref name="existingPath"/> and returns the user's choice.
    /// </summary>
    ValueTask<ConflictResolution> ShowNameConflictAsync(string existingPath);

    /// <summary>
    /// Shows a checksum-error notification for <paramref name="entryPath"/> and asks whether to continue.
    /// Returns <see langword="true"/> to continue extraction, <see langword="false"/> to abort.
    /// </summary>
    ValueTask<bool> ShowChecksumErrorAsync(string entryPath, string detail);

    /// <summary>Shows a non-blocking informational message dialog.</summary>
    ValueTask ShowMessageAsync(string title, string message);

    /// <summary>Shows the <see cref="ExtractViewModel"/> in a modal extraction-progress dialog.</summary>
    ValueTask ShowExtractDialogAsync(ExtractViewModel vm);

    /// <summary>
    /// Opens a file picker allowing multiple file selections and returns the chosen paths,
    /// or <see langword="null"/> if cancelled.
    /// </summary>
    ValueTask<IReadOnlyList<string>?> PickFilesAsync();

    /// <summary>
    /// Opens a save-file picker filtered to <c>.dar</c> archives and returns the chosen path,
    /// or <see langword="null"/> if cancelled.
    /// </summary>
    ValueTask<string?> SaveDarFileAsync();

    /// <summary>Shows the <see cref="CreateArchiveViewModel"/> in a modal archive-creation wizard dialog.</summary>
    ValueTask ShowCreateArchiveDialogAsync(CreateArchiveViewModel vm);

    /// <summary>Shows the application settings dialog.</summary>
    ValueTask ShowSettingsAsync(SettingsViewModel vm);
}
