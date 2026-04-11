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
        Version = infoVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
    }

    [RelayCommand]
    private void Close() => Closed?.Invoke();
}
