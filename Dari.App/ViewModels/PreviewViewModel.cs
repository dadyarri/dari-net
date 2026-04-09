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

    // Computed visibility helpers for compiled bindings in AXAML.
    public bool IsEmptyVisible => State == PreviewState.Empty;
    public bool IsLoadingVisible => State == PreviewState.Loading;
    public bool IsStatusVisible => State is PreviewState.Binary or PreviewState.Error or PreviewState.Encrypted;
    public bool IsBottomStatusVisible => State != PreviewState.Empty;

    partial void OnStateChanged(PreviewState value)
    {
        OnPropertyChanged(nameof(IsEmptyVisible));
        OnPropertyChanged(nameof(IsLoadingVisible));
        OnPropertyChanged(nameof(IsStatusVisible));
        OnPropertyChanged(nameof(IsBottomStatusVisible));
    }

    public PreviewViewModel(ArchiveReader reader)
    {
        _reader = reader;
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
            return;
        }

        try
        {
            await Task.Delay(250, ct).ConfigureAwait(true);
            State = PreviewState.Loading;
            StatusMessage = "";
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
            var bytes = await _reader
                .ReadDecompressedPreviewAsync(entry.Entry, ct: ct)
                .ConfigureAwait(true);

            // Discard stale result if a newer load was triggered while awaiting I/O.
            ct.ThrowIfCancellationRequested();

            State = PreviewState.Binary;
            StatusMessage = string.Format(
                LocalizationManager.Current["Preview.Binary"],
                DisplayFormatter.FormatSize((ulong)bytes.Length));
        }
        catch (InvalidOperationException)
        {
            State = PreviewState.Encrypted;
            StatusMessage = LocalizationManager.Current["Preview.Encrypted"];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
