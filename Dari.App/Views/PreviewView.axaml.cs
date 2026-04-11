using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class PreviewView : UserControl
{
    private PreviewViewModel? _vm;
    private ScrollViewer? _scrollViewer;

    public PreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _scrollViewer = this.FindControl<ScrollViewer>("TextScrollViewer");
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        UnsubscribeVm();
        DataContextChanged -= OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeVm();
        _vm = DataContext as PreviewViewModel;
        if (_vm is null)
            return;

        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void UnsubscribeVm()
    {
        if (_vm is null)
            return;

        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;

        if (e.PropertyName == nameof(PreviewViewModel.State) &&
            _vm.State is PreviewState.Text or PreviewState.Code)
        {
            _scrollViewer?.ScrollToHome();
        }
    }
}
