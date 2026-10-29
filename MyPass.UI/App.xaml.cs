using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using MyPass.Core.Data;
using MyPass.Core.Security;
using MyPass.UI.Services;
using MyPass.UI.ViewModels;
using MyPass.UI.Views;

namespace MyPass.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Force software rendering before any WPF objects are created.
        // This prevents GPU driver heap corruption (0xC0000374 / STATUS_HEAP_CORRUPTION)
        // that occurs when the D3D surface is destroyed and recreated during window resize
        // on certain WDDM driver configurations. Performance impact is negligible for a
        // non-animated, small-window application like a password manager.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        base.OnStartup(e);

        // Global safety net: log and display unhandled WPF dispatcher exceptions
        // instead of silently crashing the process.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "MyPass — Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        // Catch unobserved Task exceptions from fire-and-forget async paths.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            // No dialog here — these are background tasks; just prevent process termination.
        };

        var services = new ServiceCollection();
        ConfigureServices(services);

        ServiceProvider = services.BuildServiceProvider();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (ServiceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        catch
        {
            // Suppress cleanup errors on exit
        }
        finally
        {
            base.OnExit(e);
            Environment.Exit(0);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. MyPass.Core Services
        services.AddSingleton<ICryptoService, CryptoService>();
        services.AddSingleton<IPasswordGenerator, PasswordGenerator>();
        services.AddSingleton<IDatabaseService, DatabaseService>();

        // 2. MyPass.UI Services
        services.AddSingleton<IVaultSessionService, VaultSessionService>();
        services.AddSingleton<IClipboardService, ClipboardService>();

        // 3. ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<VaultViewModel>();

        // 4. Views
        services.AddSingleton<MainWindow>();
    }
}
