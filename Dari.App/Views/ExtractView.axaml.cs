using Avalonia.Controls;
using Avalonia.Interactivity;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class ExtractView : Window
{
    private ExtractViewModel? _vm;

    public ExtractView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.Completed -= Close;

        _vm = DataContext as ExtractViewModel;

        if (_vm is not null)
            _vm.Completed += Close;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
