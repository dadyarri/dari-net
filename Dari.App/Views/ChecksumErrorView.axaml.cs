using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class ChecksumErrorView : Window
{
    private ChecksumErrorViewModel? _vm;

    public ChecksumErrorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.Resolved -= Close;

        _vm = DataContext as ChecksumErrorViewModel;

        if (_vm is not null)
            _vm.Resolved += Close;
    }
}
