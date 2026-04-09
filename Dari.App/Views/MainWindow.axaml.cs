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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is MainWindowViewModel vm)
            _ = vm.ShutdownAsync();
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;

        // Collect all local paths from the drop.
        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        if (paths.Count == 0) return;

        // If an archive is open, any drop (files or folders) appends to it.
        if (vm.Browser is not null)
        {
            await vm.AppendFilesFromPathsAsync(paths);
            return;
        }

        // No archive open: handle .dar files and folder drops as before.
        foreach (var path in paths)
        {
            if (path.EndsWith(".dar", StringComparison.OrdinalIgnoreCase))
            {
                await vm.OpenArchiveFromPathAsync(path);
                break;
            }

            if (Directory.Exists(path))
            {
                await vm.NewArchiveFromDirectoryAsync(path);
                break;
            }
        }
    }
}
