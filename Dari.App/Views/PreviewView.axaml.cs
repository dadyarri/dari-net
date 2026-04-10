using System.ComponentModel;
using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class PreviewView : UserControl
{
    private PreviewViewModel? _vm;

    public PreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as PreviewViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PreviewViewModel.State))
            return;
        if (_vm?.State is PreviewState.Text or PreviewState.Code or PreviewState.Markdown)
            this.FindControl<ScrollViewer>("TextScrollViewer")?.ScrollToHome();
    }
}
