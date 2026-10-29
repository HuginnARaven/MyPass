using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyPass.Core.Data;
using MyPass.Core.Models;
using MyPass.Core.Security;
using MyPass.UI.Services;

namespace MyPass.UI.ViewModels;

/// <summary>
/// ViewModel managing the main vault screen with a Master-Detail view for decrypted password entries.
/// Implements <see cref="IDisposable"/> to unsubscribe from the singleton <see cref="IVaultSessionService.VaultStateChanged"/>
/// event when a Transient instance is replaced, preventing memory leaks through lingering event handlers.
/// </summary>
public partial class VaultViewModel : ObservableObject, IDisposable
{
    private readonly IDatabaseService _databaseService;
    private readonly ICryptoService _cryptoService;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IVaultSessionService _sessionService;
    private readonly IClipboardService _clipboardService;

    private List<PasswordEntry> _allEntries = new();

    public ObservableCollection<PasswordEntry> FilteredEntries { get; } = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private PasswordEntry? _selectedEntry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSelection))]
    private bool _isEditingOrCreating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailHeader))]
    [NotifyPropertyChangedFor(nameof(DeleteButtonText))]
    private bool _isNewEntry;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string _editLogin = string.Empty;

    [ObservableProperty]
    private string _editPassword = string.Empty;

    [ObservableProperty]
    private string _editNotes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordMasked))]
    private bool _isPasswordRevealed;

    [ObservableProperty]
    private string? _statusMessage;

    public bool IsPasswordMasked => !IsPasswordRevealed;
    public bool HasNoSelection => !IsEditingOrCreating;
    public string DetailHeader => IsNewEntry ? "New Password Entry" : "Edit Password Entry";
    public string DeleteButtonText => IsNewEntry ? "Cancel" : "Delete";
    public bool HasNoFilteredEntries => FilteredEntries.Count == 0;
    public string EntryCountText => $"{FilteredEntries.Count} of {_allEntries.Count} item{(_allEntries.Count == 1 ? "" : "s")}";

    public VaultViewModel(
        IDatabaseService databaseService,
        ICryptoService cryptoService,
        IPasswordGenerator passwordGenerator,
        IVaultSessionService sessionService,
        IClipboardService clipboardService)
    {
        _databaseService = databaseService;
        _cryptoService = cryptoService;
        _passwordGenerator = passwordGenerator;
        _sessionService = sessionService;
        _clipboardService = clipboardService;

        FilteredEntries.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoFilteredEntries));
            OnPropertyChanged(nameof(EntryCountText));
        };

        _sessionService.VaultStateChanged += OnVaultStateChanged;
    }

    private void OnVaultStateChanged()
    {
        if (!_sessionService.IsUnlocked)
        {
            ClearSensitiveData();
        }
    }

    /// <summary>
    /// Dynamically filters entries by Title in memory without database queries.
    /// </summary>
    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Decrypts the selected entry's fields using the vault session key.
    /// </summary>
    partial void OnSelectedEntryChanged(PasswordEntry? value)
    {
        if (value == null)
        {
            if (!IsNewEntry)
            {
                IsEditingOrCreating = false;
                ClearEditorFields();
            }
            return;
        }

        IsNewEntry = false;
        IsEditingOrCreating = true;
        IsPasswordRevealed = false;
        StatusMessage = null;

        try
        {
            byte[] key = _sessionService.GetKey();
            EditTitle = value.Title;
            EditLogin = !string.IsNullOrEmpty(value.EncryptedLogin)
                ? _cryptoService.Decrypt(value.EncryptedLogin, key)
                : string.Empty;
            EditPassword = !string.IsNullOrEmpty(value.EncryptedPassword)
                ? _cryptoService.Decrypt(value.EncryptedPassword, key)
                : string.Empty;
            EditNotes = !string.IsNullOrEmpty(value.EncryptedNotes)
                ? _cryptoService.Decrypt(value.EncryptedNotes, key)
                : string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to decrypt entry: {ex.Message}";
            ClearEditorFields();
            IsEditingOrCreating = false;
        }
    }

    /// <summary>
    /// Loads all vault entries from the database into memory and applies the current search filter.
    /// </summary>
    [RelayCommand]
    public async Task LoadEntriesAsync()
    {
        try
        {
            var entries = await _databaseService.GetAllEntriesAsync();
            _allEntries = entries.ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load entries: {ex.Message}";
        }
    }

    /// <summary>
    /// Prepares the detail editor for creating a new password entry.
    /// </summary>
    [RelayCommand]
    public void StartNewEntry()
    {
        IsNewEntry = true;
        SelectedEntry = null;
        IsEditingOrCreating = true;
        ClearEditorFields();
        IsPasswordRevealed = true;
        StatusMessage = null;
    }

    /// <summary>
    /// Validates, encrypts, and persists the entry (either creating new or updating existing).
    /// </summary>
    [RelayCommand]
    public async Task SaveEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            StatusMessage = "Title is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditPassword))
        {
            StatusMessage = "Password is required.";
            return;
        }

        byte[] key;
        try
        {
            key = _sessionService.GetKey();
        }
        catch
        {
            StatusMessage = "Vault is locked. Cannot save entry.";
            return;
        }

        try
        {
            string trimmedTitle = EditTitle.Trim();
            string encryptedPassword = _cryptoService.Encrypt(EditPassword, key);
            string? encryptedLogin = !string.IsNullOrWhiteSpace(EditLogin)
                ? _cryptoService.Encrypt(EditLogin.Trim(), key)
                : null;
            string? encryptedNotes = !string.IsNullOrWhiteSpace(EditNotes)
                ? _cryptoService.Encrypt(EditNotes.Trim(), key)
                : null;

            if (IsNewEntry)
            {
                var newEntry = new PasswordEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = trimmedTitle,
                    EncryptedLogin = encryptedLogin,
                    EncryptedPassword = encryptedPassword,
                    EncryptedNotes = encryptedNotes,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                };

                await _databaseService.AddEntryAsync(newEntry);

                // If currently searching, clear the filter if it would hide the new entry
                if (!string.IsNullOrWhiteSpace(SearchQuery) &&
                    !trimmedTitle.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                {
                    SearchQuery = string.Empty;
                }

                await LoadEntriesAsync();

                SelectedEntry = FilteredEntries.FirstOrDefault(e => e.Id == newEntry.Id)
                    ?? _allEntries.FirstOrDefault(e => e.Id == newEntry.Id);

                StatusMessage = "Entry created successfully.";
            }
            else
            {
                if (SelectedEntry == null)
                {
                    StatusMessage = "No entry selected to update.";
                    return;
                }

                string entryId = SelectedEntry.Id;
                SelectedEntry.Title = trimmedTitle;
                SelectedEntry.EncryptedLogin = encryptedLogin;
                SelectedEntry.EncryptedPassword = encryptedPassword;
                SelectedEntry.EncryptedNotes = encryptedNotes;
                SelectedEntry.UpdatedAt = DateTime.UtcNow.ToString("O");

                await _databaseService.UpdateEntryAsync(SelectedEntry);

                await LoadEntriesAsync();

                SelectedEntry = FilteredEntries.FirstOrDefault(e => e.Id == entryId)
                    ?? _allEntries.FirstOrDefault(e => e.Id == entryId);

                StatusMessage = "Entry saved successfully.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving entry: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes the currently selected entry from the database, or cancels new entry creation.
    /// </summary>
    [RelayCommand]
    public async Task DeleteEntryAsync()
    {
        if (IsNewEntry)
        {
            IsEditingOrCreating = false;
            IsNewEntry = false;
            ClearEditorFields();
            StatusMessage = null;
            return;
        }

        if (SelectedEntry == null)
        {
            return;
        }

        try
        {
            string idToDelete = SelectedEntry.Id;
            await _databaseService.DeleteEntryAsync(idToDelete);

            SelectedEntry = null;
            IsEditingOrCreating = false;
            IsNewEntry = false;
            ClearEditorFields();

            await LoadEntriesAsync();
            StatusMessage = "Entry deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete entry: {ex.Message}";
        }
    }

    /// <summary>
    /// Generates a strong random 20-character password and populates EditPassword.
    /// </summary>
    [RelayCommand]
    public void GenerateRandomPassword()
    {
        EditPassword = _passwordGenerator.GeneratePassword(20, true, true, true, true);
        IsPasswordRevealed = true;
        StatusMessage = "Generated random password.";
    }

    /// <summary>
    /// Toggles the visibility of the password field between masked and plain text.
    /// </summary>
    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordRevealed = !IsPasswordRevealed;
    }

    /// <summary>
    /// Copies the current password to the Windows clipboard with 15-second auto-clear.
    /// </summary>
    [RelayCommand]
    private void CopyPassword()
    {
        if (string.IsNullOrEmpty(EditPassword))
        {
            return;
        }

        _clipboardService.CopyWithAutoClear(EditPassword, 15);
        StatusMessage = "Password copied (auto-clears in 15s)";
    }

    /// <summary>
    /// Copies the current login/username to the Windows clipboard with 15-second auto-clear.
    /// </summary>
    [RelayCommand]
    private void CopyLogin()
    {
        if (string.IsNullOrEmpty(EditLogin))
        {
            return;
        }

        _clipboardService.CopyWithAutoClear(EditLogin, 15);
        StatusMessage = "Login copied (auto-clears in 15s)";
    }

    /// <summary>
    /// Exports a backup copy of the SQLite database to a user-selected path.
    /// </summary>
    [RelayCommand]
    private async Task ExportVaultAsync()
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "SQLite Database (*.db)|*.db|All files (*.*)|*.*",
                FileName = "mypass-backup.db",
                Title = "Export Vault Database Backup"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string destinationPath = dialog.FileName;
            string sourcePath = _databaseService.CurrentDatabasePath;

            if (!File.Exists(sourcePath))
            {
                StatusMessage = "Database file not found.";
                return;
            }

            // Flush and release SQLite connection pools
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            await Task.Run(() =>
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
            });

            StatusMessage = "Backup exported successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Clears all decrypted strings, clears in-memory entry lists, and locks the session.
    /// </summary>
    [RelayCommand]
    public void LockVault()
    {
        ClearSensitiveData();
        _sessionService.Lock();
    }

    private void ClearSensitiveData()
    {
        ClearEditorFields();
        IsEditingOrCreating = false;
        IsNewEntry = false;
        SelectedEntry = null;
        SearchQuery = string.Empty;

        _allEntries.Clear();
        FilteredEntries.Clear();
        StatusMessage = null;
    }

    private void ClearEditorFields()
    {
        EditTitle = string.Empty;
        EditLogin = string.Empty;
        EditPassword = string.Empty;
        EditNotes = string.Empty;
        IsPasswordRevealed = false;
    }

    private void ApplyFilter()
    {
        var targetList = string.IsNullOrWhiteSpace(SearchQuery)
            ? _allEntries
            : _allEntries.Where(e => e.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

        var targetSet = new HashSet<PasswordEntry>(targetList);

        // Remove entries not matching the query
        for (int i = FilteredEntries.Count - 1; i >= 0; i--)
        {
            if (!targetSet.Contains(FilteredEntries[i]))
            {
                FilteredEntries.RemoveAt(i);
            }
        }

        // Add or position matching entries in order
        for (int i = 0; i < targetList.Count; i++)
        {
            var item = targetList[i];
            if (i < FilteredEntries.Count)
            {
                if (FilteredEntries[i] != item)
                {
                    int existingIndex = FilteredEntries.IndexOf(item);
                    if (existingIndex >= 0)
                    {
                        FilteredEntries.Move(existingIndex, i);
                    }
                    else
                    {
                        FilteredEntries.Insert(i, item);
                    }
                }
            }
            else
            {
                FilteredEntries.Add(item);
            }
        }
    }

    /// <summary>
    /// Unsubscribes from the singleton <see cref="IVaultSessionService.VaultStateChanged"/> event
    /// to prevent this Transient instance from being kept alive indefinitely via the singleton's
    /// event delegate list.
    /// </summary>
    public void Dispose()
    {
        _sessionService.VaultStateChanged -= OnVaultStateChanged;
        GC.SuppressFinalize(this);
    }
}
