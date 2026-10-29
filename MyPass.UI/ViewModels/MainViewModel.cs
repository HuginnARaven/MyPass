using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using MyPass.UI.Services;

namespace MyPass.UI.ViewModels;

/// <summary>
/// Root ViewModel that orchestrates view navigation based on vault lock status.
/// Implements <see cref="IDisposable"/> to cleanly unsubscribe from the singleton
/// <see cref="IVaultSessionService.VaultStateChanged"/> event on disposal, and
/// disposes any <see cref="IDisposable"/> child ViewModels (e.g. Transient
/// <see cref="VaultViewModel"/>) when they are replaced during navigation.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IVaultSessionService _sessionService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public MainViewModel(IVaultSessionService sessionService, IServiceProvider serviceProvider)
    {
        _sessionService = sessionService;
        _serviceProvider = serviceProvider;

        _sessionService.VaultStateChanged += OnVaultStateChanged;
    }

    private void OnVaultStateChanged()
    {
        NavigateToAppropriateView();
    }

    /// <summary>
    /// Navigates to LoginViewModel if the vault is locked, or VaultViewModel if unlocked.
    /// Disposes the previous CurrentViewModel if it implements <see cref="IDisposable"/>.
    /// </summary>
    public void NavigateToAppropriateView()
    {
        var previous = CurrentViewModel;

        if (!_sessionService.IsUnlocked)
        {
            CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
        }
        else
        {
            CurrentViewModel = _serviceProvider.GetRequiredService<VaultViewModel>();
        }

        // Dispose the replaced ViewModel (e.g. transient VaultViewModel) so it
        // unsubscribes from singleton events and is eligible for GC.
        if (previous is IDisposable disposable && !ReferenceEquals(previous, CurrentViewModel))
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Unsubscribes from the singleton session event. Called when the application exits.
    /// </summary>
    public void Dispose()
    {
        _sessionService.VaultStateChanged -= OnVaultStateChanged;
        if (CurrentViewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}

