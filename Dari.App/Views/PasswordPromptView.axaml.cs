using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class PasswordPromptView : Window
{
    private PasswordPromptViewModel? _currentVm;

    public PasswordPromptView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from any previous VM to prevent duplicate handlers.
        if (_currentVm is not null)
        {
            _currentVm.Confirmed -= Close;
            _currentVm.Cancelled -= Close;
        }

        _currentVm = DataContext as PasswordPromptViewModel;

        if (_currentVm is not null)
        {
            _currentVm.Confirmed += Close;
            _currentVm.Cancelled += Close;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_currentVm is not null)
        {
            _currentVm.Confirmed -= Close;
            _currentVm.Cancelled -= Close;
            _currentVm = null;
        }
    }
}
