using CommunityToolkit.Mvvm.Input;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for a simple informational message dialog.</summary>
public sealed partial class MessageViewModel
{
    public string Title { get; }
    public string Message { get; }

    /// <summary>Raised when the user closes the dialog.</summary>
    public event Action? Closed;

    public MessageViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}
