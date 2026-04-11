using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Dari.App.ViewModels;

/// <summary>ViewModel for the About dialog.</summary>
public sealed partial class AboutViewModel : ObservableObject
{
    public string AppName => "Dari";

    public string Version { get; }

    public string Description => "Cross-platform archive manager for .dar archives";

    public string Copyright => $"© {DateTime.Now.Year} dadyarri";

    public string License => "MIT License";

    public string ProjectUrl => "https://github.com/dadyarri/dari-net";

    public string Runtime => $".NET {Environment.Version}";

    public string OperatingSystem => System.Runtime.InteropServices.RuntimeInformation.OSDescription;

    /// <summary>Raised when the dialog should close.</summary>
    public event Action? Closed;

    public AboutViewModel()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var raw = infoVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        Version = TruncateHash(raw);
    }

    /// <summary>
    /// Limits the build-metadata hash in an informational version string to 8 characters.
    /// E.g. <c>1.0.0+abcdef1234567890</c> → <c>1.0.0+abcdef12</c>.
    /// Strings without a <c>+</c> separator are returned unchanged.
    /// </summary>
    private static string TruncateHash(string version)
    {
        var plus = version.IndexOf('+');
        if (plus < 0) return version;

        var prefix = version[..(plus + 1)];   // "1.0.0+"
        var hash = version[(plus + 1)..];      // full hash or build metadata
        return hash.Length > 8 ? $"{prefix}{hash[..8]}" : version;
    }

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}
