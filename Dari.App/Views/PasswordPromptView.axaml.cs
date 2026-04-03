using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class PasswordPromptView : Window
{
    public PasswordPromptView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is PasswordPromptViewModel vm)
        {
            vm.Confirmed += Close;
            vm.Cancelled += Close;
        }
    }
}
