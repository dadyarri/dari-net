using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Helpers;
using Dari.App.Models;
using Dari.App.Services;
using Dari.Archiver.Archiving;
using Dari.Archiver.Crypto;

namespace Dari.App.ViewModels;

/// <summary>Controls the display mode of the archive browser.</summary>
public enum ViewMode { Flat, Tree }

/// <summary>Column to sort the entry list by.</summary>
public enum SortColumn { Name, Size, Date, Ratio }

/// <summary>Sort direction for the entry list.</summary>
public enum SortDirection { Ascending, Descending }

/// <summary>
/// ViewModel for the archive-browser pane.
/// Holds all <see cref="ArchiveEntryViewModel"/> items, supports searching, sorting, and
/// flat-list / directory-tree view modes.
/// </summary>
public sealed partial class ArchiveBrowserViewModel : ObservableObject, IDisposable, IAsyncDisposable
{
    private readonly ArchiveReader _reader;
    private readonly DariPassphrase? _passphrase;
    private readonly IReadOnlyList<ArchiveEntryViewModel> _allEntries;
    private readonly IDialogService _dialogService;

    // -----------------------------------------------------------------------
    // Observable properties
    // -----------------------------------------------------------------------

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private ViewMode _viewMode = ViewMode.Tree;

    [ObservableProperty]
    private SortColumn _activeSortColumn = SortColumn.Name;

    [ObservableProperty]
    private SortDirection _activeSortDirection = SortDirection.Ascending;

    [ObservableProperty]
    private ObservableCollection<ArchiveEntryViewModel> _filteredEntries = [];

    [ObservableProperty]
    private ObservableCollection<TreeNodeViewModel> _treeRootNodes = [];

    /// <summary>Preview pane state machine for the currently selected entry.</summary>
    public PreviewViewModel Preview { get; }

    [ObservableProperty]
    private ArchiveEntryViewModel? _selectedEntry;

    partial void OnSelectedEntryChanged(ArchiveEntryViewModel? value) =>
        Preview.LoadAsync(value);

    // -----------------------------------------------------------------------
    // Archive metadata
    // -----------------------------------------------------------------------

    /// <summary>UTC creation timestamp recorded in the archive header.</summary>
    public DateTimeOffset CreatedAt => _reader.CreatedAt;

    /// <summary>Total number of entries in the archive (including linked duplicates).</summary>
    public int FileCount => _allEntries.Count;

    /// <summary>Sum of original (uncompressed) sizes of all entries.</summary>
    public ulong TotalSize { get; }

    /// <summary>Sum of stored (compressed) sizes of all entries.</summary>
    public ulong TotalCompressedSize { get; }

    /// <summary>Human-readable total uncompressed size.</summary>
    public string TotalSizeDisplay => DisplayFormatter.FormatSize(TotalSize);

    /// <summary>Human-readable total compressed size.</summary>
    public string TotalCompressedSizeDisplay => DisplayFormatter.FormatSize(TotalCompressedSize);

    /// <summary>Optional passphrase retained for extraction (Phase C).</summary>
    public DariPassphrase? Passphrase => _passphrase;

    /// <summary>Localized label for the view-mode toggle button.</summary>
    public string ViewModeDisplay =>
        LocalizationManager.Current[ViewMode == ViewMode.Flat
            ? "Toolbar.ViewMode.Flat"
            : "Toolbar.ViewMode.Tree"];

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public ArchiveBrowserViewModel(ArchiveReader reader, DariPassphrase? passphrase = null,
                                   IDialogService? dialogService = null,
                                   int previewMaxMegaBytes = 10)
    {
        _reader = reader;
        _passphrase = passphrase;
        _dialogService = dialogService ?? NullDialogService.Instance;
        _allEntries = reader.Entries.Select(e => new ArchiveEntryViewModel(e)).ToList();
        Preview = new PreviewViewModel(reader, previewMaxMegaBytes);

        ulong totalSize = 0UL, totalCompressed = 0UL;
        foreach (var e in _allEntries)
        {
            totalSize += e.OriginalSize;
            totalCompressed += e.CompressedSize;
        }
        TotalSize = totalSize;
        TotalCompressedSize = totalCompressed;

        Refresh();

        // Subscribe to individual entry selection changes so that directory
        // tri-state checkboxes reflect the state of their children.
        foreach (var entry in _allEntries)
            entry.PropertyChanged += OnEntrySelectionChanged;
    }

    // -----------------------------------------------------------------------
    // Change handlers
    // -----------------------------------------------------------------------

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnActiveSortColumnChanged(SortColumn value) => Refresh();
    partial void OnActiveSortDirectionChanged(SortDirection value) => Refresh();

    partial void OnViewModeChanged(ViewMode value)
    {
        OnPropertyChanged(nameof(ViewModeDisplay));
        Refresh();
    }

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    /// <summary>Sorts the list by the named column; toggles direction if already active.</summary>
    [RelayCommand]
    private void SortBy(string columnName)
    {
        if (!Enum.TryParse<SortColumn>(columnName, out var col)) return;

        if (ActiveSortColumn == col)
            ActiveSortDirection = ActiveSortDirection == SortDirection.Ascending
                ? SortDirection.Descending
                : SortDirection.Ascending;
        else
        {
            ActiveSortColumn = col;
            ActiveSortDirection = SortDirection.Ascending;
        }
    }

    /// <summary>Toggles between flat-list and directory-tree view modes.</summary>
    [RelayCommand]
    private void ToggleViewMode() =>
        ViewMode = ViewMode == ViewMode.Flat ? ViewMode.Tree : ViewMode.Flat;

    /// <summary>Extracts all archive entries to a user-chosen directory.</summary>
    [RelayCommand]
    private async Task ExtractAllAsync()
    {
        var destination = await _dialogService.PickFolderAsync().ConfigureAwait(true);
        if (destination is null) return;

        var vm = new ExtractViewModel(_allEntries.Select(e => e.Entry).ToList(),
                                     _reader, destination, _dialogService);
        await _dialogService.ShowExtractDialogAsync(vm).ConfigureAwait(true);
        ClearAllSelections();
    }

    /// <summary>Extracts only the checked (selected) entries to a user-chosen directory.</summary>
    [RelayCommand]
    private async Task ExtractSelectedAsync()
    {
        var selected = _allEntries.Where(e => e.IsSelected).Select(e => e.Entry).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowMessageAsync(
                LocalizationManager.Current["Dialog.NoSelection.Title"],
                LocalizationManager.Current["Dialog.NoSelection.Message"]).ConfigureAwait(true);
            return;
        }

        var selectedEntries = _allEntries.Where(e => e.IsSelected).ToList();
        var selectedDirPrefixes = GetSelectedDirectoryPrefixes();

        using var optionsVm = new ExtractOptionsViewModel(selectedEntries, selectedDirPrefixes, _dialogService);
        await _dialogService.ShowExtractOptionsDialogAsync(optionsVm).ConfigureAwait(true);

        if (!optionsVm.IsConfirmed || string.IsNullOrEmpty(optionsVm.DestinationPath)) return;

        var extractDirPrefixes = optionsVm.FlatPaths ? selectedDirPrefixes : null;
        var vm = new ExtractViewModel(selected, _reader, optionsVm.DestinationPath, _dialogService,
            flatPaths: optionsVm.FlatPaths, selectedDirectoryPrefixes: extractDirPrefixes);
        await _dialogService.ShowExtractDialogAsync(vm).ConfigureAwait(true);

        ClearAllSelections();
    }

    // -----------------------------------------------------------------------
    // Entry selection → directory tri-state
    // -----------------------------------------------------------------------

    private bool _dirUpdatePending;

    private void OnEntrySelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ArchiveEntryViewModel.IsSelected)) return;

        // Coalesce multiple rapid file-selection changes (e.g. from a directory check
        // propagating to hundreds of children) into a single UI-thread update per frame.
        if (_dirUpdatePending) return;
        _dirUpdatePending = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(DeferredUpdateDirSelectionStates);
    }

    private void DeferredUpdateDirSelectionStates()
    {
        _dirUpdatePending = false;
        UpdateDirSelectionStates();
    }

    private void UpdateDirSelectionStates()
    {
        foreach (var node in TreeRootNodes)
            if (node is DirectoryNodeViewModel dir)
                UpdateDirStateRecursive(dir);
    }

    /// <summary>
    /// Depth-first traversal: updates sub-directories first, then the parent,
    /// so each directory's state reflects its already-updated children.
    /// </summary>
    private static void UpdateDirStateRecursive(DirectoryNodeViewModel dir)
    {
        foreach (var child in dir.Children.OfType<DirectoryNodeViewModel>())
            UpdateDirStateRecursive(child);

        dir.UpdateSelectionStateFromChildren();
    }

    // -----------------------------------------------------------------------
    // Refresh logic
    // -----------------------------------------------------------------------

    private void Refresh()
    {
        IEnumerable<ArchiveEntryViewModel> query = _allEntries;

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(e =>
                e.Path.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                e.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // Sort
        query = ActiveSortColumn switch
        {
            SortColumn.Name => ActiveSortDirection == SortDirection.Ascending
                ? query.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
            SortColumn.Size => ActiveSortDirection == SortDirection.Ascending
                ? query.OrderBy(e => e.OriginalSize)
                : query.OrderByDescending(e => e.OriginalSize),
            SortColumn.Date => ActiveSortDirection == SortDirection.Ascending
                ? query.OrderBy(e => e.ModifiedAt)
                : query.OrderByDescending(e => e.ModifiedAt),
            SortColumn.Ratio => ActiveSortDirection == SortDirection.Ascending
                ? query.OrderBy(e => e.CompressionRatio ?? 1.0)
                : query.OrderByDescending(e => e.CompressionRatio ?? 1.0),
            _ => query,
        };

        FilteredEntries = new ObservableCollection<ArchiveEntryViewModel>(query);
        BuildTree();
    }

    private void BuildTree()
    {
        var root = new DirectoryNodeViewModel("", "");
        foreach (var entry in FilteredEntries)
        {
            var parts = entry.Path.Split('/');
            var current = root;
            string currentPath = "";
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var segName = parts[i];
                currentPath += segName + "/";
                var segPath = currentPath; // capture for closure
                var existing = current.Children
                    .OfType<DirectoryNodeViewModel>()
                    .FirstOrDefault(n => n.Name == segName);
                if (existing is null)
                {
                    existing = new DirectoryNodeViewModel(segName, segPath);
                    current.Children.Add(existing);
                }
                current = existing;
            }
            current.Children.Add(new FileNodeViewModel(entry));
        }

        // Collect top-level items (dirs first, then bare files)
        var roots = new ObservableCollection<TreeNodeViewModel>();
        foreach (var child in root.Children)
            roots.Add(child);
        TreeRootNodes = roots;

        // Initialise directory tri-state checkboxes from the current file selection state.
        UpdateDirSelectionStates();
    }

    // -----------------------------------------------------------------------
    // Selection helpers
    // -----------------------------------------------------------------------

    private void ClearAllSelections()
    {
        foreach (var entry in _allEntries) entry.IsSelected = false;
        // Dir states are recomputed by OnEntrySelectionChanged automatically.
    }

    /// <summary>
    /// Returns the <see cref="DirectoryNodeViewModel.FullPath"/> of every directory node
    /// that is currently selected (checked). Sub-directories of already-selected directories
    /// are not included separately because the parent prefix covers them.
    /// </summary>
    private IReadOnlyList<string> GetSelectedDirectoryPrefixes()
    {
        var result = new List<string>();
        CollectSelectedDirPrefixes(TreeRootNodes, result);
        return result;
    }

    private static void CollectSelectedDirPrefixes(
        IEnumerable<TreeNodeViewModel> nodes, List<string> result)
    {
        foreach (var node in nodes.OfType<DirectoryNodeViewModel>())
        {
            if (node.IsSelected == true)
                result.Add(node.FullPath); // children are covered by this prefix
            else
                CollectSelectedDirPrefixes(node.Children, result);
        }
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        foreach (var entry in _allEntries)
            entry.PropertyChanged -= OnEntrySelectionChanged;
        Preview.Dispose();
        _reader.Dispose();
        _passphrase?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var entry in _allEntries)
            entry.PropertyChanged -= OnEntrySelectionChanged;
        Preview.Dispose();
        await _reader.DisposeAsync().ConfigureAwait(false);
        _passphrase?.Dispose();
    }
}
