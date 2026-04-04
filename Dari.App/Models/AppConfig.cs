namespace Dari.App.Models;

/// <summary>Persisted user preferences written to the platform-specific config file.</summary>
public sealed class AppConfig
{
    /// <summary>
    /// BCP-47-like language code for the UI locale (e.g. <c>"en"</c> or <c>"ru"</c>).
    /// Defaults to <c>"en"</c>.
    /// </summary>
    public string Language { get; set; } = "en";
}
