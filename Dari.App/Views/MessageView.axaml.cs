using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class MessageView : Window
{
    private MessageViewModel? _vm;

    public MessageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.Closed -= Close;

        _vm = DataContext as MessageViewModel;

        if (_vm is not null)
            _vm.Closed += Close;
    }
}
