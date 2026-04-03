using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;
using Dari.Archiver.Archiving;
using Dari.Archiver.Format;

namespace Dari.App.ViewModels;

/// <summary>
/// ViewModel for the extraction-progress dialog.
/// Drives extraction of a list of entries to a destination directory,
/// reporting progress and handling name conflicts and checksum errors.
/// </summary>
public sealed partial class ExtractViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyList<IndexEntry> _entries;
    private readonly ArchiveReader _reader;
    private readonly string _destinationPath;
    private readonly IDialogService _dialogService;
    private readonly CancellationTokenSource _cts = new();

    // -----------------------------------------------------------------------
    // Observable state
    // -----------------------------------------------------------------------

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private int _extractedCount;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isRunning = true;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _statusMessage = "";

    // -----------------------------------------------------------------------
    // Results (readable after completion)
    // -----------------------------------------------------------------------

    /// <summary>Number of files successfully extracted.</summary>
    public int ExtractedFiles { get; private set; }

    /// <summary>Whether the extraction was cancelled by the user.</summary>
    public bool WasCancelled { get; private set; }

    /// <summary>Destination directory — shown in the completion panel.</summary>
    public string DestinationPath => _destinationPath;

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised (on the UI thread) when extraction finishes or is cancelled.</summary>
    public event Action? Completed;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public ExtractViewModel(
        IReadOnlyList<IndexEntry> entries,
        ArchiveReader reader,
        string destinationPath,
        IDialogService dialogService)
    {
        _entries = entries;
        _reader = reader;
        _destinationPath = destinationPath;
        _dialogService = dialogService;
        TotalCount = entries.Count;
    }

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    [RelayCommand]
    private void Cancel()
    {
        _cts.Cancel();
        WasCancelled = true;
        StatusMessage = "Cancelling…";
    }

    /// <summary>Opens the destination directory in the system file manager.</summary>
    [RelayCommand]
    private void OpenInExplorer()
    {
        if (!Directory.Exists(_destinationPath)) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = _destinationPath,
            UseShellExecute = true,
        });
    }

    // -----------------------------------------------------------------------
    // Extraction logic
    // -----------------------------------------------------------------------

    /// <summary>
    /// Begins extraction.  Must be called from the UI thread; all I/O is async.
    /// </summary>
    public async Task StartExtractionAsync()
    {
        IsRunning = true;
        try
        {
            await ExtractEntriesAsync(_cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            StatusMessage = "Extraction cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            IsCompleted = true;

            if (!WasCancelled && string.IsNullOrEmpty(StatusMessage))
                StatusMessage = $"Done — extracted {ExtractedFiles} of {TotalCount} file(s).";

            Completed?.Invoke();
        }
    }

    private async Task ExtractEntriesAsync(CancellationToken ct)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = _entries[i];
            CurrentFile = entry.Path;

            string relPath = entry.Path.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(_destinationPath, relPath);

            // Handle name conflicts.
            if (File.Exists(fullPath))
            {
                var resolution = await _dialogService
                    .ShowNameConflictAsync(fullPath)
                    .ConfigureAwait(true);

                switch (resolution)
                {
                    case ConflictResolution.Skip:
                        ExtractedCount++;
                        Progress = (double)(i + 1) / _entries.Count;
                        continue;

                    case ConflictResolution.Rename:
                        fullPath = BuildUniquePath(fullPath);
                        break;

                    case ConflictResolution.Overwrite:
                        break;
                }
            }

            // Extract the entry.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? _destinationPath);

                await using var fs = new FileStream(
                    fullPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, bufferSize: 65536, useAsync: true);
                await _reader.ExtractAsync(entry, fs, verifyChecksum: true, ct)
                             .ConfigureAwait(true);

                File.SetLastWriteTimeUtc(fullPath, entry.ModifiedAt.UtcDateTime);
                ExtractedFiles++;
            }
            catch (InvalidDataException ex) when (ex.Message.Contains("Checksum"))
            {
                bool continueExtraction = await _dialogService
                    .ShowChecksumErrorAsync(entry.Path, ex.Message)
                    .ConfigureAwait(true);

                if (!continueExtraction) return;
            }

            ExtractedCount++;
            Progress = (double)(i + 1) / _entries.Count;
        }
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    public void Dispose() => _cts.Dispose();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Appends a numeric suffix to <paramref name="path"/> until a name that does not
    /// clash with any existing file is found.  E.g. <c>foo.txt</c> → <c>foo (1).txt</c>.
    /// </summary>
    private static string BuildUniquePath(string path)
    {
        string dir = Path.GetDirectoryName(path) ?? ".";
        string nameWithoutExt = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);

        for (int n = 1; ; n++)
        {
            string candidate = Path.Combine(dir, $"{nameWithoutExt} ({n}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
