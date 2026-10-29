namespace MyPass.Core.Models;

/// <summary>
/// Represents a stored vault entry containing encrypted credentials and metadata.
/// </summary>
public class PasswordEntry
{
    /// <summary>
    /// Unique identifier (GUID string) for the entry.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Unencrypted title or service name (e.g., "Google", "GitHub") for fast searching in the UI.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded encrypted login/username payload, or null if empty.
    /// </summary>
    public string? EncryptedLogin { get; set; }

    /// <summary>
    /// Base64-encoded encrypted password payload.
    /// </summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>
    /// Base64-encoded encrypted notes payload, or null if empty.
    /// </summary>
    public string? EncryptedNotes { get; set; }

    /// <summary>
    /// ISO-8601 UTC timestamp of entry creation.
    /// </summary>
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("O");

    /// <summary>
    /// ISO-8601 UTC timestamp of last update.
    /// </summary>
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("O");
}
