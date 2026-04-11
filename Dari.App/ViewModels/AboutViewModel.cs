using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dari.Archiver;

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
        Version = WithFormatVersion(raw);
    }

    /// <summary>
    /// Strips any existing build-metadata suffix (<c>+…</c>) and appends the max supported
    /// archive format version, e.g. <c>1.0.0-pre.abc12345</c> → <c>1.0.0-pre.abc12345+5</c>.
    /// </summary>
    private static string WithFormatVersion(string version)
    {
        var plus = version.IndexOf('+');
        var semver = plus >= 0 ? version[..plus] : version;
        return $"{semver}+{DariInfo.MaxFormatVersion}";
    }

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}
