namespace Dari.App.Helpers;

/// <summary>Shared display-formatting helpers.</summary>
internal static class DisplayFormatter
{
    /// <summary>Formats <paramref name="bytes"/> into a human-readable size string (e.g. "1.2 MB").</summary>
    public static string FormatSize(ulong bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024UL * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
    };
}
