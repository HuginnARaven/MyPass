using System.Windows;
using System.Windows.Threading;
using MyPass.UI.Services;
using MyPass.UI.ViewModels;

namespace MyPass.UI.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml with inactivity auto-lock.
/// Uses a passive timestamp approach to avoid Stop/Start timer churn on every mouse-move event,
/// which was causing heap corruption (0xC0000374) during native window resize operations.
/// A single periodic tick every 10 seconds checks elapsed idle time rather than restarting
/// the timer on every pixel of mouse movement.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IVaultSessionService _sessionService;
    private readonly DispatcherTimer _inactivityTimer;
    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);
    private DateTime _lastActivityUtc = DateTime.UtcNow;

    public MainWindow(MainViewModel viewModel, IVaultSessionService sessionService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _sessionService = sessionService;

        // Single timer that ticks every 10 s — never stopped/started on mouse-move.
        _inactivityTimer = new DispatcherTimer
        {
            Interval = CheckInterval
        };
        _inactivityTimer.Tick += OnInactivityTimerTick;

        // Only update the timestamp — do NOT restart the timer here.
        PreviewMouseMove += (_, _) => _lastActivityUtc = DateTime.UtcNow;
        PreviewKeyDown  += (_, _) => _lastActivityUtc = DateTime.UtcNow;

        Loaded += (_, _) =>
        {
            viewModel.NavigateToAppropriateView();
            _lastActivityUtc = DateTime.UtcNow;
        };

        _sessionService.VaultStateChanged += OnVaultStateChanged;
        Closed += OnWindowClosed;
    }

    private void OnVaultStateChanged()
    {
        if (_sessionService.IsUnlocked)
        {
            _lastActivityUtc = DateTime.UtcNow;
            _inactivityTimer.Start();
        }
        else
        {
            _inactivityTimer.Stop();
        }
    }

    private void OnInactivityTimerTick(object? sender, EventArgs e)
    {
        if (!_sessionService.IsUnlocked)
        {
            _inactivityTimer.Stop();
            return;
        }

        if (DateTime.UtcNow - _lastActivityUtc >= InactivityTimeout)
        {
            _inactivityTimer.Stop();
            _sessionService.Lock();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _inactivityTimer.Stop();
        _inactivityTimer.Tick -= OnInactivityTimerTick;
        _sessionService.VaultStateChanged -= OnVaultStateChanged;
    }
}
