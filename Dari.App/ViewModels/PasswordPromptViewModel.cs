using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;
using Dari.Archiver.Crypto;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for the passphrase-entry dialog shown when opening an encrypted archive.</summary>
public sealed partial class PasswordPromptViewModel : ObservableObject
{
    private Func<DariPassphrase, ValueTask<bool>>? _validator;

    [ObservableProperty]
    private string _passphrase = "";

    [ObservableProperty]
    private string _errorMessage = "";

    /// <summary><see langword="true"/> if the user confirmed with OK and the passphrase was validated.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>The validated <see cref="DariPassphrase"/>; non-null only when <see cref="IsConfirmed"/> is <see langword="true"/>.</summary>
    public DariPassphrase? VerifiedPassphrase { get; private set; }

    /// <summary>
    /// When <see langword="false"/> (ChaCha20-Poly1305 not supported on this OS), the passphrase
    /// box and OK button should be disabled and an explanatory message should be shown.
    /// </summary>
    public bool IsEncryptionSupported => CryptoCapabilities.IsEncryptionSupported;

    /// <summary>Raised when the user presses OK and the passphrase passes validation.</summary>
    public event Action? Confirmed;

    /// <summary>Raised when the user presses Cancel or closes the dialog.</summary>
    public event Action? Cancelled;

    /// <summary>
    /// Optionally sets an async validator invoked when the user clicks OK.
    /// If the validator returns <see langword="false"/> the dialog stays open and shows an error.
    /// </summary>
    public void SetValidator(Func<DariPassphrase, ValueTask<bool>> validator) =>
        _validator = validator;

    partial void OnPassphraseChanged(string value) => ErrorMessage = "";

    [RelayCommand]
    private async Task Confirm()
    {
        ErrorMessage = "";
        if (string.IsNullOrEmpty(Passphrase)) return;

        var pass = new DariPassphrase(Passphrase);

        if (_validator is not null)
        {
            bool ok;
            try
            {
                ok = await _validator(pass).ConfigureAwait(true);
            }
            catch (PlatformNotSupportedException ex)
            {
                FileLogger.Log(ex, "PasswordPromptViewModel.Confirm");
                pass.Dispose();
                ErrorMessage = LocalizationManager.Current["Password.EncryptionUnsupported"];
                return;
            }
            catch (Exception ex)
            {
                FileLogger.Log(ex, "PasswordPromptViewModel.Confirm");
                pass.Dispose();
                throw;
            }

            if (!ok)
            {
                pass.Dispose();
                ErrorMessage = "Wrong passphrase. Please try again.";
                return;
            }
        }

        VerifiedPassphrase = pass;
        IsConfirmed = true;
        Confirmed?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}

