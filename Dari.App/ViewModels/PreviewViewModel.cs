using CommunityToolkit.Mvvm.ComponentModel;
using Dari.App.Helpers;
using Dari.App.Models;
using Dari.App.Services;
using Dari.Archiver.Archiving;

namespace Dari.App.ViewModels;

public enum PreviewState { Empty, Loading, Text, Code, Image, Markdown, Binary, Error, Encrypted }

public sealed partial class PreviewViewModel : ObservableObject, IDisposable
{
    private readonly ArchiveReader _reader;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    private PreviewState _state = PreviewState.Empty;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _previewText = "";

    [ObservableProperty]
    private string _previewTypeLabel = "";

    // Computed visibility helpers for compiled bindings in AXAML.
    public bool IsEmptyVisible => State == PreviewState.Empty;
    public bool IsLoadingVisible => State == PreviewState.Loading;
    public bool IsTextVisible => State is PreviewState.Text or PreviewState.Code or PreviewState.Markdown;
    public bool IsStatusVisible => State is PreviewState.Binary or PreviewState.Error or PreviewState.Encrypted;
    public bool IsBottomStatusVisible => State is not (PreviewState.Empty or PreviewState.Loading);
    public bool IsTruncationVisible => IsTextVisible && StatusMessage != "";

    partial void OnStateChanged(PreviewState value)
    {
        OnPropertyChanged(nameof(IsEmptyVisible));
        OnPropertyChanged(nameof(IsLoadingVisible));
        OnPropertyChanged(nameof(IsTextVisible));
        OnPropertyChanged(nameof(IsStatusVisible));
        OnPropertyChanged(nameof(IsBottomStatusVisible));
        OnPropertyChanged(nameof(IsTruncationVisible));
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(IsTruncationVisible));
    }

    public int MaxPreviewMegaBytes { get; set; }

    public PreviewViewModel(ArchiveReader reader, int maxPreviewMegaBytes = 10)
    {
        _reader = reader;
        MaxPreviewMegaBytes = maxPreviewMegaBytes;
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
            State = PreviewState.Empty;
            StatusMessage = "";
            PreviewTypeLabel = "";
            return;
        }

        try
        {
            await Task.Delay(250, ct).ConfigureAwait(true);
            State = PreviewState.Loading;
            StatusMessage = "";
            PreviewTypeLabel = "";
            await LoadContentAsync(entry, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled — a newer load is on its way.
        }
    }

    private async Task LoadContentAsync(ArchiveEntryViewModel entry, CancellationToken ct)
    {
        try
        {
            var maxBytes = MaxPreviewMegaBytes * 1024 * 1024;
            var bytes = await _reader
                .ReadDecompressedPreviewAsync(entry.Entry, maxBytes, ct)
                .ConfigureAwait(true);

            // Discard stale result if a newer load was triggered while awaiting I/O.
            ct.ThrowIfCancellationRequested();

            var classifyResult = ContentClassifier.ClassifyBytes(bytes.Span, maxBytes);

            if (classifyResult.Kind == ContentKind.Binary)
            {
                PreviewTypeLabel = LocalizationManager.Current["Preview.Type.Binary"];
                State = PreviewState.Binary;
                StatusMessage = string.Format(
                    LocalizationManager.Current["Preview.Binary"],
                    DisplayFormatter.FormatSize((ulong)bytes.Length));
                return;
            }

            // Route to Text / Code / Markdown based on extension.
            var previewState = ContentClassifier.ClassifyForPreview(bytes.Span, entry.Extension, maxBytes);
            var typeKey = previewState switch
            {
                PreviewState.Code => "Preview.Type.Code",
                PreviewState.Markdown => "Preview.Type.Markdown",
                _ => "Preview.Type.Text",
            };
            PreviewText = ContentClassifier.DecodeText(bytes.Span, classifyResult.Encoding);
            PreviewTypeLabel = $"{LocalizationManager.Current[typeKey]} · {classifyResult.Encoding}";
            State = previewState;

            var truncated = entry.Entry.OriginalSize > (ulong)maxBytes;
            StatusMessage = truncated
                ? string.Format(LocalizationManager.Current["Preview.Truncated"], MaxPreviewMegaBytes)
                : "";
        }
        catch (InvalidOperationException)
        {
            PreviewTypeLabel = LocalizationManager.Current["Preview.Type.Encrypted"];
            State = PreviewState.Encrypted;
            StatusMessage = LocalizationManager.Current["Preview.Encrypted"];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PreviewTypeLabel = LocalizationManager.Current["Preview.Type.Error"];
            State = PreviewState.Error;
            StatusMessage = string.Format(
                LocalizationManager.Current["Preview.Error"], ex.Message);
        }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
