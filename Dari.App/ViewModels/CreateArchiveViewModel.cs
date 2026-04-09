using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Services;
using Dari.Archiver.Archiving;
using Dari.Archiver.Compression;
using Dari.Archiver.Crypto;
using Dari.Archiver.Format;
using Dari.Archiver.Ignoring;

namespace Dari.App.ViewModels;

/// <summary>
/// ViewModel for the three-step "Create Archive" wizard.
/// <list type="bullet">
///   <item>Step 1 — Source: pick a directory or individual files; preview the tree.</item>
///   <item>Step 2 — Options: compression algorithm, deduplication, encryption.</item>
///   <item>Step 3 — Destination: output path and creation progress.</item>
/// </list>
/// </summary>
public sealed partial class CreateArchiveViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<string>? _selectedFiles;

    // -----------------------------------------------------------------------
    // Step management (1=Source, 2=Options, 3=Destination)
    // -----------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(IsReadyToCreate))]
    private int _currentStep = 1;

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;

    /// <summary>
    /// <see langword="true"/> when the user is on step 3 but creation has not yet started or completed.
    /// Controls the visibility of the Create and Back buttons in step 3.
    /// </summary>
    public bool IsReadyToCreate => IsStep3 && !IsCreating && !IsCompleted;

    // -----------------------------------------------------------------------
    // Step 1 — Source
    // -----------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourcePath))]
    private string _sourcePath = "";

    [ObservableProperty]
    private ObservableCollection<string> _previewFiles = [];

    public bool HasSourcePath => !string.IsNullOrWhiteSpace(SourcePath);

    // -----------------------------------------------------------------------
    // Step 2 — Options
    // -----------------------------------------------------------------------

    public IReadOnlyList<string> CompressionOptions { get; } =
        ["Auto", "Brotli", "Zstandard", "LZMA", "None"];

    [ObservableProperty]
    private string _selectedCompression = "Auto";

    [ObservableProperty]
    private bool _enableDeduplication = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEncryptionFields))]
    private bool _enableEncryption;

    [ObservableProperty]
    private string _encryptionPassphrase = "";

    [ObservableProperty]
    private string _encryptionPassphraseConfirm = "";

    [ObservableProperty]
    private string _optionsError = "";

    public bool ShowEncryptionFields => EnableEncryption;

    // -----------------------------------------------------------------------
    // Step 3 — Destination + progress
    // -----------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputPath))]
    private string _outputPath = "";

    public bool HasOutputPath => !string.IsNullOrWhiteSpace(OutputPath);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadyToCreate))]
    private bool _isCreating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadyToCreate))]
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

    /// <summary>Path of the successfully created archive; non-null after a successful run.</summary>
    public string? CreatedArchivePath { get; private set; }

    /// <summary>Whether the user cancelled the creation.</summary>
    public bool WasCancelled { get; private set; }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised when the wizard should be closed (completed, cancelled, or dismissed).</summary>
    public event Action? Closed;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public CreateArchiveViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    // -----------------------------------------------------------------------
    // Step 1 — commands
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task BrowseSourceDirectoryAsync()
    {
        var path = await _dialogService.PickFolderAsync().ConfigureAwait(true);
        if (path is null) return;

        _selectedFiles = null;
        SourcePath = path;
        await RefreshPreviewAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BrowseSourceFilesAsync()
    {
        var paths = await _dialogService.PickFilesAsync().ConfigureAwait(true);
        if (paths is null || paths.Count == 0) return;

        _selectedFiles = paths;
        SourcePath = Path.GetDirectoryName(paths[0]) ?? paths[0];
        PreviewFiles = new ObservableCollection<string>(paths.Select(p => Path.GetFileName(p)!));
    }

    private async Task RefreshPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || !Directory.Exists(SourcePath))
        {
            PreviewFiles = [];
            return;
        }

        var relPaths = await Task.Run(() =>
        {
            var filter = GitIgnoreFilter.Load(SourcePath);
            var root = new DirectoryInfo(SourcePath);
            return CollectRelativePaths(root, root, filter);
        }).ConfigureAwait(true);

        PreviewFiles = new ObservableCollection<string>(relPaths);
    }

    private static List<string> CollectRelativePaths(
        DirectoryInfo root, DirectoryInfo current, IIgnoreFilter filter)
    {
        var result = new List<string>();

        foreach (var fi in current.EnumerateFiles())
        {
            string rel = Path.GetRelativePath(root.FullName, fi.FullName)
                             .Replace(Path.DirectorySeparatorChar, '/');
            if (!filter.ShouldIgnore(rel, isDirectory: false))
                result.Add(rel);
        }

        foreach (var sub in current.EnumerateDirectories())
        {
            string rel = Path.GetRelativePath(root.FullName, sub.FullName)
                             .Replace(Path.DirectorySeparatorChar, '/');
            if (!filter.ShouldIgnore(rel, isDirectory: true))
                result.AddRange(CollectRelativePaths(root, sub, filter));
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Step navigation
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task NextStepAsync()
    {
        if (CurrentStep == 1)
        {
            if (!HasSourcePath)
            {
                await _dialogService.ShowMessageAsync(
                    LocalizationManager.Current["Dialog.NoSource.Title"],
                    LocalizationManager.Current["Dialog.NoSource.Message"]).ConfigureAwait(true);
                return;
            }
            CurrentStep = 2;
        }
        else if (CurrentStep == 2)
        {
            if (!ValidateOptions()) return;
            CurrentStep = 3;
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    private bool ValidateOptions()
    {
        OptionsError = "";

        if (!EnableEncryption) return true;

        if (string.IsNullOrEmpty(EncryptionPassphrase))
        {
            OptionsError = LocalizationManager.Current["Validation.NoPassphrase"];
            return false;
        }

        if (EncryptionPassphrase != EncryptionPassphraseConfirm)
        {
            OptionsError = LocalizationManager.Current["Validation.PassphrasesMismatch"];
            return false;
        }

        return true;
    }

    // -----------------------------------------------------------------------
    // Step 3 — commands
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var path = await _dialogService.SaveDarFileAsync().ConfigureAwait(true);
        if (path is not null) OutputPath = path;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (!HasOutputPath)
        {
            await _dialogService.ShowMessageAsync(
                LocalizationManager.Current["Dialog.NoOutputPath.Title"],
                LocalizationManager.Current["Dialog.NoOutputPath.Message"]).ConfigureAwait(true);
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsCreating = true;
        WasCancelled = false;
        StatusMessage = "";

        try
        {
            await RunCreationAsync(_cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            WasCancelled = true;
            StatusMessage = LocalizationManager.Current["Status.CreationCancelled"];
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationManager.Current.Format("Status.Error", ex.Message);
        }
        finally
        {
            IsCreating = false;
            IsCompleted = true;
            if (!WasCancelled && string.IsNullOrEmpty(StatusMessage))
                StatusMessage = LocalizationManager.Current.Format("Status.Done.Create", DoneCount);
        }
    }

    private async Task RunCreationAsync(CancellationToken ct)
    {
        CompressorRegistry registry = SelectedCompression switch
        {
            "Brotli" => CompressorRegistry.CreateFixed(CompressionMethod.Brotli),
            "Zstandard" => CompressorRegistry.CreateFixed(CompressionMethod.Zstandard),
            "LZMA" => CompressorRegistry.CreateFixed(CompressionMethod.Lzma),
            "None" => CompressorRegistry.CreateFixed(CompressionMethod.None),
            _ => CompressorRegistry.Default,
        };

        DariPassphrase? passphrase = EnableEncryption && !string.IsNullOrEmpty(EncryptionPassphrase)
            ? new DariPassphrase(EncryptionPassphrase)
            : null;

        var progress = new Progress<(int done, int total, string currentFile)>(p =>
        {
            DoneCount = p.done;
            TotalCount = p.total;
            CurrentFile = p.currentFile;
            Progress = p.total > 0 ? (double)p.done / p.total : 0;
        });

        try
        {
            await using var writer = await ArchiveWriter.CreateAsync(
                OutputPath,
                compressors: registry,
                passphrase: passphrase,
                enableDeduplication: EnableDeduplication,
                ct: ct).ConfigureAwait(true);

            if (_selectedFiles is not null)
                await AddIndividualFilesAsync(writer, progress, ct).ConfigureAwait(true);
            else
                await writer.AddDirectoryAsync(SourcePath, progress: progress, ct: ct)
                            .ConfigureAwait(true);

            await writer.FinalizeAsync(ct).ConfigureAwait(true);
        }
        finally
        {
            passphrase?.Dispose();
        }

        CreatedArchivePath = OutputPath;
    }

    private async Task AddIndividualFilesAsync(
        ArchiveWriter writer,
        IProgress<(int done, int total, string currentFile)> progress,
        CancellationToken ct)
    {
        var files = _selectedFiles!;
        int total = files.Count;
        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            string archivePath = Path.GetFileName(files[i]);
            progress.Report((i, total, archivePath));
            await writer.AddAsync(files[i], archivePath, ct).ConfigureAwait(true);
            progress.Report((i + 1, total, archivePath));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsCreating)
        {
            _cts?.Cancel();
            WasCancelled = true;
        }
        else
        {
            WasCancelled = true;
            Closed?.Invoke();
        }
    }

    [RelayCommand]
    private void CloseWizard() => Closed?.Invoke();

    [RelayCommand]
    private void OpenInExplorer()
    {
        string? dir = Path.GetDirectoryName(OutputPath);
        if (dir is not null && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true,
            });
        }
    }

    // -----------------------------------------------------------------------
    // Pre-populate source path (used by drag-and-drop from MainWindow)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sets the source directory to <paramref name="path"/> and refreshes the file preview.
    /// Call this before showing the dialog when the user drops a folder onto the window.
    /// </summary>
    public async Task SetSourceDirectoryAsync(string path)
    {
        _selectedFiles = null;
        SourcePath = path;
        await RefreshPreviewAsync().ConfigureAwait(true);
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
