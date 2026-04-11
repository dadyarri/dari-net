using Avalonia;
using Avalonia.Controls;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class PasswordPromptView : Window
{
    private PasswordPromptViewModel? _currentVm;

    public PasswordPromptView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    /// <summary>
    /// After layout the window has its real size; recompute position so it is correctly
    /// centered on the owner regardless of which monitor the owner sits on.
    /// Avalonia's built-in CenterOwner startup location calculates position before
    /// the first layout pass, producing wrong results when SizeToContent is used.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Owner is not Window owner) return;

        var screen = owner.Screens?.ScreenFromWindow(owner);
        var scale = screen?.Scaling ?? 1.0;
        var ownerW = owner.ClientSize.Width * scale;
        var ownerH = owner.ClientSize.Height * scale;
        var myW = ClientSize.Width * scale;
        var myH = ClientSize.Height * scale;

        Position = new PixelPoint(
            owner.Position.X + (int)((ownerW - myW) / 2),
            owner.Position.Y + (int)((ownerH - myH) / 2));
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from any previous VM to prevent duplicate handlers.
        if (_currentVm is not null)
        {
            _currentVm.Confirmed -= Close;
            _currentVm.Cancelled -= Close;
        }

        _currentVm = DataContext as PasswordPromptViewModel;

        if (_currentVm is not null)
        {
            _currentVm.Confirmed += Close;
            _currentVm.Cancelled += Close;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_currentVm is not null)
        {
            _currentVm.Confirmed -= Close;
            _currentVm.Cancelled -= Close;
            _currentVm = null;
        }
    }
}
