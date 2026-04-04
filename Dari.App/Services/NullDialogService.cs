using Dari.Archiver.Crypto;
using Dari.App.ViewModels;

namespace Dari.App.Services;

/// <summary>
/// No-op implementation of <see cref="IDialogService"/> used as a safe fallback
/// when the browser is instantiated without a dialog service (e.g. in unit tests).
/// All picker methods return <see langword="null"/>; conflict dialogs return the most
/// conservative default.
/// </summary>
internal sealed class NullDialogService : IDialogService
{
    public static readonly NullDialogService Instance = new();

    private NullDialogService() { }

    public ValueTask<string?> OpenDarFileAsync() => new((string?)null);

    public ValueTask<DariPassphrase?> ShowPasswordPromptAsync(
        Func<DariPassphrase, ValueTask<bool>>? validator = null) => new((DariPassphrase?)null);

    public ValueTask<string?> PickFolderAsync() => new((string?)null);

    public ValueTask<ConflictResolution> ShowNameConflictAsync(string existingPath) =>
        new(ConflictResolution.Skip);

    public ValueTask<bool> ShowChecksumErrorAsync(string entryPath, string detail) =>
        new(false);

    public ValueTask ShowMessageAsync(string title, string message) => ValueTask.CompletedTask;

    public ValueTask ShowExtractDialogAsync(ExtractViewModel vm) => ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<string>?> PickFilesAsync() => new((IReadOnlyList<string>?)null);

    public ValueTask<string?> SaveDarFileAsync() => new((string?)null);

    public ValueTask ShowCreateArchiveDialogAsync(CreateArchiveViewModel vm) => ValueTask.CompletedTask;

    public ValueTask ShowSettingsAsync(SettingsViewModel vm) => ValueTask.CompletedTask;

    public ValueTask ShowExtractOptionsDialogAsync(ExtractOptionsViewModel vm) => ValueTask.CompletedTask;
}
