using Dapper;
using Microsoft.Data.Sqlite;
using MyPass.Core.Models;

namespace MyPass.Core.Data;

/// <summary>
/// Manages SQLite database access using Microsoft.Data.Sqlite and Dapper.
/// All connections use <c>await using</c> to ensure the async dispose path is followed,
/// which properly returns connections to the pool and avoids OS file-lock accumulation.
/// </summary>
public class DatabaseService : IDatabaseService
{
    /// <inheritdoc />
    public string CurrentDatabasePath { get; private set; }

    public DatabaseService(string? databasePath = null)
    {
        CurrentDatabasePath = databasePath ?? GetDefaultDatabasePath();
    }

    /// <inheritdoc />
    public void SetDatabasePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        // Release all pooled connections before switching paths so the OS file lock
        // on the previous database file (including -wal and -shm sidecar files) is
        // fully released before the new path is set.
        SqliteConnection.ClearAllPools();
        CurrentDatabasePath = Path.GetFullPath(filePath);
    }

    private static string GetDefaultDatabasePath()
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataFolder, "MyPass", "mypass.db");
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={CurrentDatabasePath}");
    }

    /// <inheritdoc />
    public async Task InitializeDatabaseAsync()
    {
        string? directory = Path.GetDirectoryName(CurrentDatabasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        const string sql = """
            CREATE TABLE IF NOT EXISTS SystemConfig (
                ConfigId INTEGER PRIMARY KEY,
                KdfSalt TEXT NOT NULL,
                KdfIterations INTEGER NOT NULL,
                MasterPasswordVerifier TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Entries (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                EncryptedLogin TEXT NULL,
                EncryptedPassword TEXT NOT NULL,
                EncryptedNotes TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """;

        await connection.ExecuteAsync(sql).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsVaultInitializedAsync()
    {
        if (!File.Exists(CurrentDatabasePath))
        {
            return false;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        const string tableCheckSql = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='SystemConfig';";
        int tableCount = await connection.ExecuteScalarAsync<int>(tableCheckSql).ConfigureAwait(false);
        if (tableCount == 0)
        {
            return false;
        }

        const string configCheckSql = "SELECT COUNT(1) FROM SystemConfig WHERE ConfigId = 1;";
        int count = await connection.ExecuteScalarAsync<int>(configCheckSql).ConfigureAwait(false);
        return count > 0;
    }

    /// <inheritdoc />
    public async Task<SystemConfig?> GetSystemConfigAsync()
    {
        await using var connection = CreateConnection();
        const string sql = """
            SELECT ConfigId, KdfSalt, KdfIterations, MasterPasswordVerifier
            FROM SystemConfig
            WHERE ConfigId = 1;
            """;
        return await connection.QuerySingleOrDefaultAsync<SystemConfig>(sql).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveSystemConfigAsync(SystemConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.ConfigId = 1;

        await using var connection = CreateConnection();
        const string sql = """
            INSERT INTO SystemConfig (ConfigId, KdfSalt, KdfIterations, MasterPasswordVerifier)
            VALUES (@ConfigId, @KdfSalt, @KdfIterations, @MasterPasswordVerifier)
            ON CONFLICT(ConfigId) DO UPDATE SET
                KdfSalt = excluded.KdfSalt,
                KdfIterations = excluded.KdfIterations,
                MasterPasswordVerifier = excluded.MasterPasswordVerifier;
            """;

        await connection.ExecuteAsync(sql, config).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PasswordEntry>> GetAllEntriesAsync()
    {
        await using var connection = CreateConnection();
        const string sql = """
            SELECT Id, Title, EncryptedLogin, EncryptedPassword, EncryptedNotes, CreatedAt, UpdatedAt
            FROM Entries
            ORDER BY Title COLLATE NOCASE ASC;
            """;
        return await connection.QueryAsync<PasswordEntry>(sql).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PasswordEntry?> GetEntryByIdAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = CreateConnection();
        const string sql = """
            SELECT Id, Title, EncryptedLogin, EncryptedPassword, EncryptedNotes, CreatedAt, UpdatedAt
            FROM Entries
            WHERE Id = @Id;
            """;
        return await connection.QuerySingleOrDefaultAsync<PasswordEntry>(sql, new { Id = id }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddEntryAsync(PasswordEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Id))
        {
            entry.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(entry.CreatedAt))
        {
            entry.CreatedAt = DateTime.UtcNow.ToString("O");
        }

        if (string.IsNullOrWhiteSpace(entry.UpdatedAt))
        {
            entry.UpdatedAt = DateTime.UtcNow.ToString("O");
        }

        await using var connection = CreateConnection();
        const string sql = """
            INSERT INTO Entries (Id, Title, EncryptedLogin, EncryptedPassword, EncryptedNotes, CreatedAt, UpdatedAt)
            VALUES (@Id, @Title, @EncryptedLogin, @EncryptedPassword, @EncryptedNotes, @CreatedAt, @UpdatedAt);
            """;

        await connection.ExecuteAsync(sql, entry).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateEntryAsync(PasswordEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);

        entry.UpdatedAt = DateTime.UtcNow.ToString("O");

        await using var connection = CreateConnection();
        const string sql = """
            UPDATE Entries
            SET Title = @Title,
                EncryptedLogin = @EncryptedLogin,
                EncryptedPassword = @EncryptedPassword,
                EncryptedNotes = @EncryptedNotes,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id;
            """;

        await connection.ExecuteAsync(sql, entry).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = CreateConnection();
        const string sql = "DELETE FROM Entries WHERE Id = @Id;";
        await connection.ExecuteAsync(sql, new { Id = id }).ConfigureAwait(false);
    }
}
