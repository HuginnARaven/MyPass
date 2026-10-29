using System.IO;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyPass.Core.Data;
using MyPass.Core.Models;
using MyPass.Core.Security;
using MyPass.UI.Services;

namespace MyPass.UI.ViewModels;

/// <summary>
/// ViewModel managing vault initialization, unlock, creation, and database file selection.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly ICryptoService _cryptoService;
    private readonly IVaultSessionService _sessionService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyPropertyChangedFor(nameof(TitleText))]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    [NotifyPropertyChangedFor(nameof(ShowConfirmPassword))]
    private bool _isVaultInitialized;

    [ObservableProperty]
    private string _masterPassword = string.Empty;

    [ObservableProperty]
    private string _confirmMasterPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentDbPath = string.Empty;

    public string ActionButtonText => IsVaultInitialized ? "Unlock Vault" : "Create Vault";
    public string TitleText => IsVaultInitialized ? "Unlock Vault" : "Create New Vault";
    public string SubtitleText => IsVaultInitialized
        ? "Enter your master password to decrypt and access your vault."
        : "Choose a strong master password (minimum 8 characters) to secure your vault.";

    public bool ShowConfirmPassword => !IsVaultInitialized;
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsNotBusy => !IsBusy;

    public LoginViewModel(
        IDatabaseService databaseService,
        ICryptoService cryptoService,
        IVaultSessionService sessionService)
    {
        _databaseService = databaseService;
        _cryptoService = cryptoService;
        _sessionService = sessionService;
    }

    /// <summary>
    /// Checks if vault already exists at current database path, and resets inputs.
    /// </summary>
    [RelayCommand]
    public async Task InitializeAsync()
    {
        ErrorMessage = null;
        MasterPassword = string.Empty;
        ConfirmMasterPassword = string.Empty;

        try
        {
            CurrentDbPath = _databaseService.CurrentDatabasePath;
            IsVaultInitialized = await _databaseService.IsVaultInitializedAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Database error: {ex.Message}";
        }
    }

    /// <summary>
    /// Authenticates with the master password to unlock an existing vault or creates a new one.
    /// </summary>
    [RelayCommand]
    public async Task UnlockOrCreateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            ErrorMessage = "Master password is required.";
            return;
        }

        IsBusy = true;
        try
        {
            if (!IsVaultInitialized)
            {
                // Create New Vault Mode
                if (MasterPassword.Length < 8)
                {
                    ErrorMessage = "Master password must be at least 8 characters long.";
                    return;
                }

                if (MasterPassword != ConfirmMasterPassword)
                {
                    ErrorMessage = "Passwords do not match.";
                    return;
                }

                string password = MasterPassword;

                // Run key derivation off UI thread to keep the application responsive
                var (saltBytes, keyBytes, verifierBase64) = await Task.Run(() =>
                {
                    byte[] salt = _cryptoService.GenerateSalt(32);
                    byte[] key = _cryptoService.DeriveKey(password, salt, 600_000);
                    string verifier = _cryptoService.CreateVerifier(key);
                    return (salt, key, verifier);
                });

                var config = new SystemConfig
                {
                    ConfigId = 1,
                    KdfSalt = Convert.ToBase64String(saltBytes),
                    KdfIterations = 600_000,
                    MasterPasswordVerifier = verifierBase64
                };

                await _databaseService.InitializeDatabaseAsync();
                await _databaseService.SaveSystemConfigAsync(config);

                MasterPassword = string.Empty;
                ConfirmMasterPassword = string.Empty;

                _sessionService.SetKey(keyBytes);
                CryptographicOperations.ZeroMemory(keyBytes);
                CryptographicOperations.ZeroMemory(saltBytes);
            }
            else
            {
                // Unlock Existing Vault Mode
                var config = await _databaseService.GetSystemConfigAsync();
                if (config == null)
                {
                    ErrorMessage = "Configuration not found. The database may be uninitialized.";
                    return;
                }

                string password = MasterPassword;

                var (isValid, keyBytes) = await Task.Run(() =>
                {
                    byte[] salt = Convert.FromBase64String(config.KdfSalt);
                    byte[] key = _cryptoService.DeriveKey(password, salt, config.KdfIterations);
                    bool verified = _cryptoService.VerifyKey(key, config.MasterPasswordVerifier);

                    if (!verified)
                    {
                        CryptographicOperations.ZeroMemory(key);
                        return (false, Array.Empty<byte>());
                    }

                    return (true, key);
                });

                if (!isValid)
                {
                    ErrorMessage = "Invalid master password.";
                    return;
                }

                MasterPassword = string.Empty;
                ConfirmMasterPassword = string.Empty;

                _sessionService.SetKey(keyBytes);
                CryptographicOperations.ZeroMemory(keyBytes);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Authentication error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Opens a file dialog to switch or create another database file.
    /// </summary>
    [RelayCommand]
    public async Task SelectDatabaseFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SQLite Database (*.db;*.sqlite)|*.db;*.sqlite|All files (*.*)|*.*",
            CheckFileExists = false,
            Title = "Select or Create Vault Database",
            InitialDirectory = !string.IsNullOrWhiteSpace(CurrentDbPath)
                ? Path.GetDirectoryName(CurrentDbPath)
                : null
        };

        if (dialog.ShowDialog() == true)
        {
            _databaseService.SetDatabasePath(dialog.FileName);
            await InitializeAsync();
        }
    }
}
