using System.Collections.Frozen;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.App.Helpers;
using Dari.App.Models;
using Dari.App.Services;
using Dari.Archiver.Archiving;

namespace Dari.App.ViewModels;

public enum PreviewState { Empty, Loading, Text, Code, Image, Markdown, Binary, Error, Encrypted }

public sealed partial class PreviewViewModel : ObservableObject, IDisposable
{
    private const string PreviewTempDirectoryName = "dari-preview";
    private const string MarkdownFrontMatterDelimiter = "---";
    private static readonly string MarkdownFrontMatterStartLf = $"{MarkdownFrontMatterDelimiter}\n";
    private static readonly string MarkdownFrontMatterStartCrLf = $"{MarkdownFrontMatterDelimiter}\r\n";
    // MarkdownScrollViewer silently fails to render large content; fall back to Text above this byte limit.
    private const int MarkdownByteLimit = 40_960;
    private const double ImageZoomStep = 0.1;
    private const double ImageZoomMin = 0.1;
    private const double ImageZoomMax = 4;

    private static readonly FrozenSet<string> ImageExtensions =
        FrozenSet.ToFrozenSet([".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"]);

    private readonly ArchiveReader _reader;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExtractAndOpenCommand))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ZoomOutImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetImageZoomCommand))]
    private PreviewState _state = PreviewState.Empty;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _previewText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ZoomInImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ZoomOutImageCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetImageZoomCommand))]
    private Bitmap? _previewBitmap;


    [ObservableProperty]
    private string _monospaceFontFamily = "Monospace";

    [ObservableProperty]
    private double _monospaceFontSize = 12;

    [ObservableProperty]
    private double _imageScale = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExtractAndOpenCommand))]
    private ArchiveEntryViewModel? _currentEntry;

    // Status bar: translated type word (e.g. "Text") — updates on language change.
    [ObservableProperty]
    private string _previewTypeName = "";

    // Status bar: optional value appended after the type name (e.g. " · UTF-8") — empty for non-text.
    [ObservableProperty]
    private string _previewTypeEncoding = "";

    // Status bar: value shown next to the truncation label (e.g. "10 MB") — updates on language change.
    [ObservableProperty]
    private string _truncationDisplay = "";

    // Intermediate state for re-translation on language switch.
    private string _typeLabelKey = "";
    private bool _isTruncated;
    private int _truncationMb;

    // Computed visibility helpers for compiled bindings in AXAML.
    public bool IsEmptyVisible => State == PreviewState.Empty;
    public bool IsLoadingVisible => State == PreviewState.Loading;
    public bool IsTextVisible => State is PreviewState.Text or PreviewState.Code;
    public bool IsMarkdownVisible => State == PreviewState.Markdown;
    public bool IsImageVisible => State == PreviewState.Image;
    public bool IsStatusVisible => State is PreviewState.Binary or PreviewState.Error or PreviewState.Encrypted;
    public bool IsBottomStatusVisible => State is not (PreviewState.Empty or PreviewState.Loading);
    public bool IsTruncationVisible => (State is PreviewState.Text or PreviewState.Code or PreviewState.Markdown) && _isTruncated;

    partial void OnStateChanged(PreviewState value)
    {
        OnPropertyChanged(nameof(IsEmptyVisible));
        OnPropertyChanged(nameof(IsLoadingVisible));
        OnPropertyChanged(nameof(IsTextVisible));
        OnPropertyChanged(nameof(IsMarkdownVisible));
        OnPropertyChanged(nameof(IsImageVisible));
        OnPropertyChanged(nameof(IsStatusVisible));
        OnPropertyChanged(nameof(IsBottomStatusVisible));
        OnPropertyChanged(nameof(IsTruncationVisible));
    }

    public int MaxPreviewMegaBytes { get; set; }

    public string ImageScaleDisplay => $"{ImageScale * 100:0}%";

    partial void OnImageScaleChanged(double value) => OnPropertyChanged(nameof(ImageScaleDisplay));

    public PreviewViewModel(
        ArchiveReader reader,
        int maxPreviewMegaBytes = 10,
        string? monospaceFontFamily = null,
        double monospaceFontSize = 12)
    {
        _reader = reader;
        MaxPreviewMegaBytes = maxPreviewMegaBytes;
        MonospaceFontFamily = string.IsNullOrWhiteSpace(monospaceFontFamily)
            ? "Monospace"
            : monospaceFontFamily;
        MonospaceFontSize = monospaceFontSize > 0 ? monospaceFontSize : 12;
        LocalizationManager.Current.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_typeLabelKey != "")
            PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
        if (_isTruncated)
            TruncationDisplay = string.Format(LocalizationManager.Current["Preview.Truncated"], _truncationMb);
    }

    public void LoadAsync(ArchiveEntryViewModel? entry)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        _ = LoadInternalAsync(entry, ct);
    }

    private async Task LoadInternalAsync(ArchiveEntryViewModel? entry, CancellationToken ct)
    {
        if (entry is null)
        {
            CurrentEntry = null;
            State = PreviewState.Empty;
            StatusMessage = "";
            PreviewText = "";
            ResetStatusBarFields();
            ClearPreviewBitmap();
            ImageScale = 1;
            return;
        }

        try
        {
            await Task.Delay(250, ct).ConfigureAwait(true);
            State = PreviewState.Loading;
            StatusMessage = "";
            PreviewText = "";
            ResetStatusBarFields();
            await LoadContentAsync(entry, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled — a newer load is on its way.
        }
    }

    private void ResetStatusBarFields()
    {
        _typeLabelKey = "";
        _isTruncated = false;
        _truncationMb = 0;
        PreviewTypeName = "";
        PreviewTypeEncoding = "";
        TruncationDisplay = "";
        OnPropertyChanged(nameof(IsTruncationVisible));
    }

    private void ClearPreviewBitmap()
    {
        var old = PreviewBitmap;
        PreviewBitmap = null;
        old?.Dispose();
    }

    private async Task LoadContentAsync(ArchiveEntryViewModel entry, CancellationToken ct)
    {
        try
        {
            CurrentEntry = entry;
            var maxBytes = MaxPreviewMegaBytes * 1024 * 1024;
            var bytes = await _reader
                .ReadDecompressedPreviewAsync(entry.Entry, maxBytes, ct)
                .ConfigureAwait(true);

            // Discard stale result if a newer load was triggered while awaiting I/O.
            ct.ThrowIfCancellationRequested();

            var ext = entry.Extension.ToLowerInvariant();
            if (ImageExtensions.Contains(ext))
            {
                try
                {
                    var old = PreviewBitmap;
                    using var ms = new MemoryStream(bytes.ToArray());
                    PreviewBitmap = new Bitmap(ms);
                    old?.Dispose();
                    PreviewText = "";
                    _typeLabelKey = "Preview.Type.Image";
                    PreviewTypeEncoding = "";
                    _isTruncated = false;
                    TruncationDisplay = "";
                    OnPropertyChanged(nameof(IsTruncationVisible));
                    ImageScale = 1;
                    State = PreviewState.Image;
                    return;
                }
                catch (Exception ex)
                {
                    ClearPreviewBitmap();
                    _typeLabelKey = "Preview.Type.Error";
                    PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
                    PreviewTypeEncoding = "";
                    State = PreviewState.Error;
                    StatusMessage = string.Format(LocalizationManager.Current["Preview.ImageDecodeFailed"], ex.Message);
                    return;
                }
            }

            ClearPreviewBitmap();
            var classifyResult = ContentClassifier.ClassifyBytes(bytes.Span, maxBytes);

            if (classifyResult.Kind == ContentKind.Binary)
            {
                PreviewText = "";
                _typeLabelKey = "Preview.Type.Binary";
                PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
                PreviewTypeEncoding = "";
                State = PreviewState.Binary;
                StatusMessage = string.Format(
                    LocalizationManager.Current["Preview.Binary"],
                    DisplayFormatter.FormatSize((ulong)bytes.Length));
                return;
            }

            // Route to Text / Code / Markdown based on extension.
            var detectedState = ContentClassifier.ClassifyForPreview(
                bytes.Span,
                entry.Extension,
                Path.GetFileName(entry.Entry.Path),
                maxBytes);
            _typeLabelKey = detectedState switch
            {
                PreviewState.Code => "Preview.Type.Code",
                PreviewState.Markdown => "Preview.Type.Markdown",
                _ => "Preview.Type.Text",
            };
            var decodedText = ContentClassifier.DecodeText(bytes.Span, classifyResult.Encoding);
            // MarkdownScrollViewer fails to render large content; fall back to plain text.
            if (detectedState == PreviewState.Markdown && bytes.Length > MarkdownByteLimit)
                detectedState = PreviewState.Text;
            PreviewText = detectedState == PreviewState.Markdown
                ? StripMarkdownFrontMatter(decodedText)
                : decodedText;
            PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
            PreviewTypeEncoding = $" · {classifyResult.Encoding}";
            State = detectedState;

            _isTruncated = entry.Entry.OriginalSize > (ulong)maxBytes;
            if (_isTruncated)
            {
                _truncationMb = MaxPreviewMegaBytes;
                TruncationDisplay = string.Format(LocalizationManager.Current["Preview.Truncated"], _truncationMb);
            }
            OnPropertyChanged(nameof(IsTruncationVisible));
        }
        catch (InvalidOperationException)
        {
            ClearPreviewBitmap();
            PreviewText = "";
            _typeLabelKey = "Preview.Type.Encrypted";
            PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
            PreviewTypeEncoding = "";
            State = PreviewState.Encrypted;
            StatusMessage = LocalizationManager.Current["Preview.Encrypted"];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ClearPreviewBitmap();
            PreviewText = "";
            _typeLabelKey = "Preview.Type.Error";
            PreviewTypeEncoding = "";
            State = PreviewState.Error;
            StatusMessage = string.Format(
                LocalizationManager.Current["Preview.Error"], ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExtractAndOpen))]
    private async Task ExtractAndOpenAsync(CancellationToken ct)
    {
        if (CurrentEntry is null)
            return;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), PreviewTempDirectoryName);
            Directory.CreateDirectory(tempDir);
            CleanupOldTempPreviewFiles(tempDir);

            var fileName = SanitizeFileName(Path.GetFileName(CurrentEntry.Entry.Path));
            var destination = Path.Combine(tempDir, $"{Guid.NewGuid():N}_{fileName}");

            await _reader.ExtractToFileAsync(CurrentEntry.Entry, destination, ct: ct).ConfigureAwait(true);

            Process.Start(new ProcessStartInfo
            {
                FileName = destination,
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _typeLabelKey = "Preview.Type.Error";
            PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
            PreviewTypeEncoding = "";
            State = PreviewState.Error;
            StatusMessage = string.Format(LocalizationManager.Current["Preview.Error"], ex.Message);
        }
    }

    private bool CanExtractAndOpen() =>
        CurrentEntry is not null && State is not PreviewState.Empty and not PreviewState.Loading;

    [RelayCommand(CanExecute = nameof(CanAdjustImageZoom))]
    private void ZoomInImage()
    {
        var next = ImageScale + ImageZoomStep;
        ImageScale = next > ImageZoomMax ? ImageZoomMax : next;
    }

    [RelayCommand(CanExecute = nameof(CanAdjustImageZoom))]
    private void ZoomOutImage()
    {
        var next = ImageScale - ImageZoomStep;
        ImageScale = next < ImageZoomMin ? ImageZoomMin : next;
    }

    [RelayCommand(CanExecute = nameof(CanAdjustImageZoom))]
    private void ResetImageZoom() => ImageScale = 1;

    private bool CanAdjustImageZoom() => State == PreviewState.Image && PreviewBitmap is not null;

    private static string StripMarkdownFrontMatter(string text)
    {
        if (!text.StartsWith(MarkdownFrontMatterStartLf, StringComparison.Ordinal) &&
            !text.StartsWith(MarkdownFrontMatterStartCrLf, StringComparison.Ordinal))
            return text;

        var newline = text.StartsWith(MarkdownFrontMatterStartCrLf, StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
        var start = MarkdownFrontMatterDelimiter.Length + newline.Length;
        var endMarker = $"{newline}{MarkdownFrontMatterDelimiter}{newline}";
        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0)
            return text;

        return text[(end + endMarker.Length)..];
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "preview.bin";

        Span<char> invalid = stackalloc char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        foreach (var c in invalid)
            fileName = fileName.Replace(c, '_');

        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');

        return fileName;
    }

    private static void CleanupOldTempPreviewFiles(string tempDir)
    {
        var cutoff = DateTime.UtcNow.AddDays(-1);

        foreach (var file in Directory.EnumerateFiles(tempDir))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors; preview/open should still proceed.
            }
        }
    }

    public void Dispose()
    {
        LocalizationManager.Current.LanguageChanged -= OnLanguageChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        ClearPreviewBitmap();
    }
}
