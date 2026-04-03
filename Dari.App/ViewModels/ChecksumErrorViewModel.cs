using CommunityToolkit.Mvvm.Input;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for the checksum-error dialog shown when an extracted file fails verification.</summary>
public sealed partial class ChecksumErrorViewModel
{
    public string EntryPath { get; }
    public string Detail { get; }

    /// <summary><see langword="true"/> if the user chose to continue extraction despite the error.</summary>
    public bool ShouldContinue { get; private set; }

    /// <summary>Raised when the user has made a choice and the dialog should close.</summary>
    public event Action? Resolved;

    public ChecksumErrorViewModel(string entryPath, string detail)
    {
        EntryPath = entryPath;
        Detail = detail;
    }

    [RelayCommand]
    private void Continue()
    {
        ShouldContinue = true;
        Resolved?.Invoke();
    }

    [RelayCommand]
    private void Abort()
    {
        ShouldContinue = false;
        Resolved?.Invoke();
    }
}
