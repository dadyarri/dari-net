namespace Dari.App.Models;

/// <summary>Persisted user preferences written to the platform-specific config file.</summary>
public sealed class AppConfig
{
    /// <summary>
    /// BCP-47-like language code for the UI locale (e.g. <c>"en"</c> or <c>"ru"</c>).
    /// Defaults to <c>"en"</c>.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// UI theme: <c>"System"</c> (follow OS), <c>"Light"</c>, or <c>"Dark"</c>.
    /// Defaults to <c>"System"</c>.
    /// </summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Maximum number of megabytes loaded into the preview pane.
    /// Defaults to <c>10</c> MB.
    /// </summary>
    public int PreviewMaxMegaBytes { get; set; } = 10;
}
