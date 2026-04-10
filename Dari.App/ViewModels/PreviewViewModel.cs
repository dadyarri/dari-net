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
    public bool IsTextVisible => State is PreviewState.Text or PreviewState.Code or PreviewState.Markdown;
    public bool IsStatusVisible => State is PreviewState.Binary or PreviewState.Error or PreviewState.Encrypted;
    public bool IsBottomStatusVisible => State is not (PreviewState.Empty or PreviewState.Loading);
    public bool IsTruncationVisible => IsTextVisible && _isTruncated;

    partial void OnStateChanged(PreviewState value)
    {
        OnPropertyChanged(nameof(IsEmptyVisible));
        OnPropertyChanged(nameof(IsLoadingVisible));
        OnPropertyChanged(nameof(IsTextVisible));
        OnPropertyChanged(nameof(IsStatusVisible));
        OnPropertyChanged(nameof(IsBottomStatusVisible));
        OnPropertyChanged(nameof(IsTruncationVisible));
    }

    public int MaxPreviewMegaBytes { get; set; }

    public PreviewViewModel(ArchiveReader reader, int maxPreviewMegaBytes = 10)
    {
        _reader = reader;
        MaxPreviewMegaBytes = maxPreviewMegaBytes;
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
            State = PreviewState.Empty;
            StatusMessage = "";
            ResetStatusBarFields();
            return;
        }

        try
        {
            await Task.Delay(250, ct).ConfigureAwait(true);
            State = PreviewState.Loading;
            StatusMessage = "";
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
            var detectedState = ContentClassifier.ClassifyForPreview(bytes.Span, entry.Extension, entry.Name, maxBytes);
            _typeLabelKey = detectedState switch
            {
                PreviewState.Code => "Preview.Type.Code",
                PreviewState.Markdown => "Preview.Type.Markdown",
                _ => "Preview.Type.Text",
            };
            PreviewText = ContentClassifier.DecodeText(bytes.Span, classifyResult.Encoding);
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
            _typeLabelKey = "Preview.Type.Encrypted";
            PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
            PreviewTypeEncoding = "";
            State = PreviewState.Encrypted;
            StatusMessage = LocalizationManager.Current["Preview.Encrypted"];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _typeLabelKey = "Preview.Type.Error";
            PreviewTypeName = LocalizationManager.Current[_typeLabelKey];
            PreviewTypeEncoding = "";
            State = PreviewState.Error;
            StatusMessage = string.Format(
                LocalizationManager.Current["Preview.Error"], ex.Message);
        }
    }

    public void Dispose()
    {
        LocalizationManager.Current.LanguageChanged -= OnLanguageChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
