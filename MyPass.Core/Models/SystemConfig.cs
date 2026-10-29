namespace MyPass.Core.Models;

/// <summary>
/// Represents the vault configuration, including KDF parameters and the master password verifier.
/// </summary>
public class SystemConfig
{
    /// <summary>
    /// Unique identifier for the configuration record (always 1 for single-row config).
    /// </summary>
    public int ConfigId { get; set; } = 1;

    /// <summary>
    /// Base64-encoded cryptographically random salt used for key derivation.
    /// </summary>
    public string KdfSalt { get; set; } = string.Empty;

    /// <summary>
    /// Number of PBKDF2 iterations used to derive the encryption key (default 600,000).
    /// </summary>
    public int KdfIterations { get; set; } = 600_000;

    /// <summary>
    /// Base64-encoded SHA-256 hash of the derived key used to verify the master password.
    /// </summary>
    public string MasterPasswordVerifier { get; set; } = string.Empty;
}
