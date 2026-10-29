using MyPass.Core.Models;

namespace MyPass.Core.Data;

/// <summary>
/// Defines database operations for vault configuration and encrypted password entries.
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Gets the current SQLite database file path.
    /// </summary>
    string CurrentDatabasePath { get; }

    /// <summary>
    /// Sets the path to the SQLite database file to allow switching or importing vaults.
    /// </summary>
    /// <param name="filePath">Absolute or relative file path to the database.</param>
    void SetDatabasePath(string filePath);

    /// <summary>
    /// Initializes the SQLite database, creating tables if they do not exist.
    /// </summary>
    Task InitializeDatabaseAsync();

    /// <summary>
    /// Checks if the vault has been initialized with a master configuration.
    /// </summary>
    Task<bool> IsVaultInitializedAsync();

    /// <summary>
    /// Retrieves the single system configuration record (ConfigId = 1).
    /// </summary>
    Task<SystemConfig?> GetSystemConfigAsync();

    /// <summary>
    /// Inserts or replaces the system configuration record (ConfigId = 1).
    /// </summary>
    Task SaveSystemConfigAsync(SystemConfig config);

    /// <summary>
    /// Retrieves all password entries ordered by Title ascending.
    /// </summary>
    Task<IEnumerable<PasswordEntry>> GetAllEntriesAsync();

    /// <summary>
    /// Retrieves a single password entry by its unique identifier.
    /// </summary>
    Task<PasswordEntry?> GetEntryByIdAsync(string id);

    /// <summary>
    /// Inserts a new password entry into the database.
    /// </summary>
    Task AddEntryAsync(PasswordEntry entry);

    /// <summary>
    /// Updates an existing password entry and refreshes the UpdatedAt timestamp.
    /// </summary>
    Task UpdateEntryAsync(PasswordEntry entry);

    /// <summary>
    /// Deletes a password entry by its unique identifier.
    /// </summary>
    Task DeleteEntryAsync(string id);
}
