using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class NameConflictView : Window
{
    private NameConflictViewModel? _vm;

    public NameConflictView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.Resolved -= Close;

        _vm = DataContext as NameConflictViewModel;

        if (_vm is not null)
            _vm.Resolved += Close;
    }
}
