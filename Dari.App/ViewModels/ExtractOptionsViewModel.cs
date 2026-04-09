using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Models;
using Dari.App.Services;

namespace Dari.App.ViewModels;

/// <summary>One row in the extraction preview list.</summary>
public sealed record ExtractPreviewItem(string DestinationPath);

/// <summary>
/// ViewModel for the "Extract Selected" options dialog.
/// Lets the user choose a destination folder, toggle flat-path mode, and preview
/// how each selected entry will be placed on disk before extraction starts.
/// </summary>
public sealed partial class ExtractOptionsViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyList<ArchiveEntryViewModel> _selectedEntries;
    private readonly IReadOnlyList<string> _selectedDirPrefixes;
    private readonly IDialogService _dialogService;

    // -----------------------------------------------------------------------
    // Observable properties
    // -----------------------------------------------------------------------

    [ObservableProperty]
    private string _destinationPath = "";

    [ObservableProperty]
    private bool _flatPaths;

    [ObservableProperty]
    private ObservableCollection<ExtractPreviewItem> _previewItems = [];

    // -----------------------------------------------------------------------
    // Output (read after dialog closes)
    // -----------------------------------------------------------------------

    /// <summary><c>true</c> when the user confirmed the dialog; <c>false</c> when cancelled.</summary>
    public bool IsConfirmed { get; private set; }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised when the dialog should be closed.</summary>
    public event Action? Closed;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public ExtractOptionsViewModel(
        IReadOnlyList<ArchiveEntryViewModel> selectedEntries,
        IReadOnlyList<string> selectedDirPrefixes,
        IDialogService dialogService)
    {
        _selectedEntries = selectedEntries;
        _selectedDirPrefixes = selectedDirPrefixes;
        _dialogService = dialogService;
        UpdatePreview();
    }

    // -----------------------------------------------------------------------
    // Change handlers
    // -----------------------------------------------------------------------

    partial void OnDestinationPathChanged(string value)
    {
        ConfirmCommand.NotifyCanExecuteChanged();
        UpdatePreview();
    }

    partial void OnFlatPathsChanged(bool value) => UpdatePreview();

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        var path = await _dialogService.PickFolderAsync().ConfigureAwait(true);
        if (path is not null) DestinationPath = path;
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        IsConfirmed = true;
        Closed?.Invoke();
    }

    private bool CanConfirm() => !string.IsNullOrEmpty(DestinationPath);

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();

    // -----------------------------------------------------------------------
    // Preview
    // -----------------------------------------------------------------------

    private void UpdatePreview()
    {
        var items = new ObservableCollection<ExtractPreviewItem>();

        foreach (var entry in _selectedEntries)
        {
            string relPath = FlatPaths
                ? ExtractViewModel.ComputeFlatPath(entry.Path, _selectedDirPrefixes)
                : entry.Path;

            string destPath = string.IsNullOrEmpty(DestinationPath)
                ? relPath
                : Path.Combine(DestinationPath, relPath.Replace('/', Path.DirectorySeparatorChar));

            items.Add(new ExtractPreviewItem(destPath));
        }

        PreviewItems = items;
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    public void Dispose() { }
}
