using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for the name-conflict resolution dialog shown when a target file already exists.</summary>
public sealed partial class NameConflictViewModel
{
    /// <summary>Full path to the existing file that conflicts with the extraction target.</summary>
    public string ExistingPath { get; }

    /// <summary>Resolution chosen by the user; set when the dialog closes.</summary>
    public ConflictResolution Resolution { get; private set; } = ConflictResolution.Skip;

    /// <summary>Raised when the user has made a choice and the dialog should close.</summary>
    public event Action? Resolved;

    public NameConflictViewModel(string existingPath) => ExistingPath = existingPath;

    [RelayCommand]
    private void Overwrite()
    {
        Resolution = ConflictResolution.Overwrite;
        Resolved?.Invoke();
    }

    [RelayCommand]
    private void Skip()
    {
        Resolution = ConflictResolution.Skip;
        Resolved?.Invoke();
    }

    [RelayCommand]
    private void Rename()
    {
        Resolution = ConflictResolution.Rename;
        Resolved?.Invoke();
    }
}
