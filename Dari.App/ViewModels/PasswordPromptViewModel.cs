using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for the passphrase-entry dialog shown when opening an encrypted archive.</summary>
public sealed partial class PasswordPromptViewModel : ObservableObject
{
    [ObservableProperty]
    private string _passphrase = "";

    /// <summary><see langword="true"/> if the user confirmed with OK; <see langword="false"/> if cancelled.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>Raised when the user presses OK.</summary>
    public event Action? Confirmed;

    /// <summary>Raised when the user presses Cancel or closes the dialog.</summary>
    public event Action? Cancelled;

    [RelayCommand]
    private void Confirm()
    {
        IsConfirmed = true;
        Confirmed?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
