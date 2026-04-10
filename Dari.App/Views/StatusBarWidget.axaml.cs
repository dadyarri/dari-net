using Avalonia;
using Avalonia.Controls;

namespace Dari.App.Views;

public partial class StatusBarWidget : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusBarWidget, string>(nameof(Label), defaultValue: "");

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<StatusBarWidget, string>(nameof(Value), defaultValue: "");

    public static readonly StyledProperty<bool> ShowBulletBeforeProperty =
        AvaloniaProperty.Register<StatusBarWidget, bool>(nameof(ShowBulletBefore), defaultValue: false);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool ShowBulletBefore
    {
        get => GetValue(ShowBulletBeforeProperty);
        set => SetValue(ShowBulletBeforeProperty, value);
    }

    public StatusBarWidget()
    {
        InitializeComponent();
    }
}
