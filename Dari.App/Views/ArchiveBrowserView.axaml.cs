using Avalonia.Controls;
using Dari.App.Models;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class ArchiveBrowserView : UserControl
{
    public ArchiveBrowserView()
    {
        InitializeComponent();

        FlatGrid.SelectionChanged += (_, _) =>
            SyncEntry(FlatGrid.SelectedItem as ArchiveEntryViewModel);

        TreeViewControl.SelectionChanged += (_, _) =>
            SyncEntry((TreeViewControl.SelectedItem as FileNodeViewModel)?.Entry);
    }

    private void SyncEntry(ArchiveEntryViewModel? e)
    {
        if (DataContext is ArchiveBrowserViewModel vm)
            vm.SelectedEntry = e;
    }
}
