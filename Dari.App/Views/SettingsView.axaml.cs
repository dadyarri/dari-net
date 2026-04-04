using Avalonia.Markup.Xaml;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class SettingsView : Avalonia.Controls.Window
{
    public SettingsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is SettingsViewModel vm)
            vm.Closed += Close;
    }
}
