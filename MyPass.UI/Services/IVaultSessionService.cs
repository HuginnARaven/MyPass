namespace MyPass.UI.Services;

/// <summary>
/// Manages the derived encryption key in memory while the vault is unlocked.
/// </summary>
public interface IVaultSessionService
{
    /// <summary>
    /// Gets whether the vault is currently unlocked and the encryption key is present.
    /// </summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Occurs when the vault transitions between unlocked and locked states.
    /// </summary>
    event Action? VaultStateChanged;

    /// <summary>
    /// Stores a copy of the 32-byte encryption key in memory and transitions the vault to unlocked.
    /// </summary>
    /// <param name="key">The derived 32-byte encryption key.</param>
    void SetKey(byte[] key);

    /// <summary>
    /// Retrieves the current encryption key if the vault is unlocked.
    /// </summary>
    /// <returns>The 32-byte encryption key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the vault is locked.</exception>
    byte[] GetKey();

    /// <summary>
    /// Securely clears the encryption key from memory and transitions the vault to locked.
    /// </summary>
    void Lock();
}
