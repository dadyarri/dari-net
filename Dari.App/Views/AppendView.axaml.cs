using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class AppendView : Window
{
    private AppendViewModel? _vm;

    public AppendView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.Closed -= Close;

        _vm = DataContext as AppendViewModel;

        if (_vm is not null)
            _vm.Closed += Close;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.Closed -= Close;
            _vm = null;
        }
    }
}

