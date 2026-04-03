using System;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Dari.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Dari";

    [ObservableProperty]
    private string _statusText = "Ready";

    [RelayCommand]
    private void OpenArchive()
    {
        // Phase B: file dialog + ArchiveReader
    }

    [RelayCommand]
    private void CloseArchive()
    {
        // Phase B: close the open archive
    }

    [RelayCommand]
    private void About()
    {
        // Phase G: show About dialog
    }

    [RelayCommand]
    private void Exit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
