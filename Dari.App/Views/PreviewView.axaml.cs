using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using Dari.App.ViewModels;
using TextMateSharp.Grammars;

namespace Dari.App.Views;

public partial class PreviewView : UserControl
{
    private PreviewViewModel? _vm;
    private ScrollViewer? _scrollViewer;
    private RegistryOptions? _registryOptions;
    private TextMate.Installation? _textMate;

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
        _textMate?.Dispose();
        _textMate = null;
        DataContextChanged -= OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeVm();
        _vm = DataContext as PreviewViewModel;
        if (_vm is null)
            return;

        _vm.PropertyChanged += OnVmPropertyChanged;
        UpdateCodeEditor(_vm);
    }

    private void UnsubscribeVm()
    {
        if (_vm is null)
            return;

        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = null;
    }

    private void EnsureTextMate()
    {
        if (_textMate is not null)
            return;

        var theme = Application.Current?.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeName.DarkPlus
            : ThemeName.LightPlus;

        _registryOptions ??= new RegistryOptions(theme);
        _textMate = CodeEditor.InstallTextMate(_registryOptions);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null)
            return;

        if (e.PropertyName == nameof(PreviewViewModel.State))
        {
            if (_vm.State == PreviewState.Text)
                _scrollViewer?.ScrollToHome();
            if (_vm.State == PreviewState.Code)
                UpdateCodeEditor(_vm);
            return;
        }

        if (e.PropertyName is nameof(PreviewViewModel.PreviewText) or nameof(PreviewViewModel.TextMateScope))
            UpdateCodeEditor(_vm);
    }

    private void UpdateCodeEditor(PreviewViewModel vm)
    {
        if (vm.State != PreviewState.Code)
            return;

        EnsureTextMate();
        CodeEditor.Document = new TextDocument(vm.PreviewText ?? "");
        if (vm.TextMateScope is { } scope)
            _textMate?.SetGrammar(scope);
        CodeEditor.ScrollToLine(1);
    }
}
