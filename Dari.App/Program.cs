using Avalonia;
using Avalonia.Threading;
using Dari.App.Services;
using System;

namespace Dari.App;

internal sealed class Program
{
    // Avalonia configuration; do not remove.
    [STAThread]
    public static void Main(string[] args)
    {
        // Attach global exception handlers before anything else so that crashes
        // during startup (including the encrypted-archive passphrase flow on Windows)
        // are written to Logs/errors.log alongside the executable.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            var builder = BuildAvaloniaApp();

            // Avalonia UI-thread unhandled exceptions are available once the UIThread dispatcher
            // is running, which happens inside StartWithClassicDesktopLifetime below.
            Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;

            builder.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            FileLogger.Log(ex, "Program.Main");
            throw;
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            FileLogger.Log(ex, "AppDomain.UnhandledException");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        FileLogger.Log(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }

    private static void OnUiThreadUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FileLogger.Log(e.Exception, "Dispatcher.UIThread.UnhandledException");
        // Do not set e.Handled = true — let Avalonia show its error dialog / crash normally.
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
