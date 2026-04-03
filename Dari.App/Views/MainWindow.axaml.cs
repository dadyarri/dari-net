using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Dari.App.ViewModels;

namespace Dari.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var files = e.Data.GetFiles();
        if (files is null) return;

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is not null &&
                path.EndsWith(".dar", StringComparison.OrdinalIgnoreCase))
            {
                await vm.OpenArchiveFromPathAsync(path);
                break;
            }
        }
    }
}
