namespace Dari.App.Services;

/// <summary>
/// Minimal file-based error logger.  Writes timestamped entries to
/// <c>&lt;installation-directory&gt;/Logs/errors.log</c> (i.e. alongside the executable).
/// All methods are fire-and-forget safe: any I/O errors are swallowed silently so that
/// the logger itself never crashes the application.
/// </summary>
public static class FileLogger
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "Logs", "errors.log");

    private static readonly object _lock = new();

    /// <summary>Appends a timestamped error entry for <paramref name="exception"/>.</summary>
    /// <param name="exception">The exception to record.</param>
    /// <param name="context">
    ///   Optional free-text context (e.g. method or operation name) that helps identify
    ///   where the error originated.
    /// </param>
    public static void Log(Exception exception, string context = "")
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"[{DateTimeOffset.UtcNow:u}] {(string.IsNullOrEmpty(context) ? "" : $"({context}) ")}{exception.GetType().FullName}: {exception.Message}");
            if (exception.StackTrace is { } st)
                sb.AppendLine(st);
            if (exception.InnerException is { } inner)
            {
                sb.AppendLine($"  ---> {inner.GetType().FullName}: {inner.Message}");
                if (inner.StackTrace is { } ist)
                    sb.AppendLine(ist);
            }
            sb.AppendLine();

            lock (_lock)
                File.AppendAllText(LogPath, sb.ToString());
        }
        catch
        {
            // Logger must never throw.
        }
    }

    /// <summary>Appends a plain informational message (no exception).</summary>
    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var line = $"[{DateTimeOffset.UtcNow:u}] {message}{Environment.NewLine}";
            lock (_lock)
                File.AppendAllText(LogPath, line);
        }
        catch
        {
            // Logger must never throw.
        }
    }
}
