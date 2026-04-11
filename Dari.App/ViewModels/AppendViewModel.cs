using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;
using Dari.Archiver.Archiving;
using Dari.Archiver.Crypto;
using Dari.Archiver.Ignoring;

namespace Dari.App.ViewModels;

/// <summary>Source item (file or directory) queued for appending to the archive.</summary>
public sealed record SourceItem(string FullPath, bool IsDirectory)
{
    /// <summary>Display label shown in the selection list.</summary>
    public string DisplayName => IsDirectory
        ? $"[folder] {Path.GetFileName(FullPath)}"
        : Path.GetFileName(FullPath);
}

/// <summary>
/// ViewModel for the "Append Files to Archive" dialog (Phase E).
/// <para>
/// Lets the user choose files and/or directories to append to the currently open archive,
/// shows a progress bar during the append operation, and reports the result on completion.
/// </para>
/// </summary>
public sealed partial class AppendViewModel : ObservableObject, IDisposable
{
    private readonly string _archivePath;
    private readonly DariPassphrase? _passphrase;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;

    // -----------------------------------------------------------------------
    // Selection phase
    // -----------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(IsSelecting))]
    private ObservableCollection<SourceItem> _sourceItems = [];

    /// <summary>True when at least one item has been queued for appending.</summary>
    public bool HasSelection => SourceItems.Count > 0;

    // -----------------------------------------------------------------------
    // Progress / state
    // -----------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelecting))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelecting))]
    private bool _isCompleted;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private int _doneCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private string _statusMessage = "";

    /// <summary>True while the user is still selecting files (not running, not completed).</summary>
    public bool IsSelecting => !IsRunning && !IsCompleted;

    // -----------------------------------------------------------------------
    // Result
    // -----------------------------------------------------------------------

    /// <summary>Number of files successfully written to the archive; populated after a successful run.</summary>
    public int AppendedCount { get; private set; }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised when the dialog should be closed (completed or cancelled).</summary>
    public event Action? Closed;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public AppendViewModel(string archivePath, DariPassphrase? passphrase, IDialogService dialogService)
    {
        _archivePath = archivePath;
        _passphrase = passphrase;
        _dialogService = dialogService;
    }

    // -----------------------------------------------------------------------
    // Commands — file selection
    // -----------------------------------------------------------------------

    /// <summary>Opens a multi-file picker and adds the chosen files to the queue.</summary>
    [RelayCommand]
    private async Task BrowseFilesAsync()
    {
        var paths = await _dialogService.PickFilesAsync().ConfigureAwait(true);
        if (paths is null || paths.Count == 0) return;

        foreach (var p in paths)
            if (!SourceItems.Any(x => x.FullPath == p))
                SourceItems.Add(new SourceItem(p, IsDirectory: false));

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelecting));
    }

    /// <summary>Opens a folder picker and adds the chosen directory to the queue.</summary>
    [RelayCommand]
    private async Task BrowseDirectoryAsync()
    {
        var path = await _dialogService.PickAppendFolderAsync().ConfigureAwait(true);
        if (path is null) return;

        if (!SourceItems.Any(x => x.FullPath == path))
            SourceItems.Add(new SourceItem(path, IsDirectory: true));

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelecting));
    }

    /// <summary>Removes <paramref name="item"/> from the selection list.</summary>
    [RelayCommand]
    private void RemoveItem(SourceItem item)
    {
        SourceItems.Remove(item);
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelecting));
    }

    // -----------------------------------------------------------------------
    // Commands — append
    // -----------------------------------------------------------------------

    /// <summary>Starts the append operation.</summary>
    [RelayCommand]
    private async Task AppendAsync()
    {
        if (!HasSelection) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsRunning = true;
        StatusMessage = "";

        try
        {
            await RunAppendAsync(_cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = LocalizationManager.Current["Status.AppendCancelled"];
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex, "AppendViewModel.StartAppendAsync");
            StatusMessage = LocalizationManager.Current.Format("Status.Error", ex.Message);
        }
        finally
        {
            IsRunning = false;
            IsCompleted = true;
            if (string.IsNullOrEmpty(StatusMessage))
                StatusMessage = LocalizationManager.Current.Format("Status.Done.Append", AppendedCount);
        }
    }

    private async Task RunAppendAsync(CancellationToken ct)
    {
        // Collect (sourcePath, archivePath) pairs on a background thread.
        var filePairs = await Task.Run(() => CollectFilePairs(SourceItems), ct).ConfigureAwait(true);
        int total = filePairs.Count;
        TotalCount = total;
        DoneCount = 0;
        Progress = 0;

        await using var appender = await ArchiveAppender.OpenAsync(
            _archivePath, _passphrase, ct).ConfigureAwait(true);

        int done = 0;
        foreach (var (sourcePath, archivePath) in filePairs)
        {
            ct.ThrowIfCancellationRequested();
            CurrentFile = archivePath;
            DoneCount = done;
            Progress = total > 0 ? (double)done / total : 0;

            await appender.AddAsync(sourcePath, archivePath, ct).ConfigureAwait(true);
            done++;
        }

        DoneCount = done;
        Progress = 1;
        CurrentFile = "";

        await appender.FinalizeAsync(ct).ConfigureAwait(true);
        AppendedCount = done;
    }

    private static List<(string sourcePath, string archivePath)> CollectFilePairs(
        IEnumerable<SourceItem> items)
    {
        var result = new List<(string, string)>();

        foreach (var item in items)
        {
            if (item.IsDirectory)
            {
                var root = new DirectoryInfo(item.FullPath);
                if (!root.Exists) continue;

                var filter = GitIgnoreFilter.Load(item.FullPath);
                CollectFromDirectory(root, root, filter, result);
            }
            else
            {
                if (!File.Exists(item.FullPath)) continue;
                result.Add((item.FullPath, Path.GetFileName(item.FullPath)));
            }
        }

        return result;
    }

    private static void CollectFromDirectory(
        DirectoryInfo root,
        DirectoryInfo current,
        IIgnoreFilter filter,
        List<(string, string)> result)
    {
        foreach (var fi in current.EnumerateFiles())
        {
            string rel = Path.GetRelativePath(root.FullName, fi.FullName)
                             .Replace(Path.DirectorySeparatorChar, '/');
            if (!filter.ShouldIgnore(rel, isDirectory: false))
                result.Add((fi.FullName, rel));
        }

        foreach (var sub in current.EnumerateDirectories())
        {
            string rel = Path.GetRelativePath(root.FullName, sub.FullName)
                             .Replace(Path.DirectorySeparatorChar, '/');
            if (!filter.ShouldIgnore(rel, isDirectory: true))
                CollectFromDirectory(root, sub, filter, result);
        }
    }

    /// <summary>Cancels the running operation, or closes the dialog when idle.</summary>
    [RelayCommand]
    private void Cancel()
    {
        if (IsRunning)
            _cts?.Cancel();
        else
            Closed?.Invoke();
    }

    /// <summary>Closes the dialog after a completed (or failed) run.</summary>
    [RelayCommand]
    private void CloseDialog() => Closed?.Invoke();

    // -----------------------------------------------------------------------
    // Pre-population (drag & drop)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds the given file/directory paths to the selection list.
    /// Called when the user drops items onto an open archive browser.
    /// </summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            bool isDir = Directory.Exists(p);
            if (!SourceItems.Any(x => x.FullPath == p))
                SourceItems.Add(new SourceItem(p, isDir));
        }
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelecting));
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

