using Avalonia.Markup.Xaml;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class ExtractOptionsView : Avalonia.Controls.Window
{
    public ExtractOptionsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ExtractOptionsViewModel vm)
            vm.Closed += Close;
    }
}
