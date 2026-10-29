using System.Security.Cryptography;

namespace MyPass.UI.Services;

/// <summary>
/// Singleton service that holds the derived 32-byte encryption key in memory while the vault is unlocked
/// and securely wipes it upon lock or exit.
/// </summary>
public class VaultSessionService : IVaultSessionService, IDisposable
{
    private const int KeySizeInBytes = 32;
    private readonly object _lock = new();
    private byte[]? _key;

    /// <inheritdoc />
    public bool IsUnlocked
    {
        get
        {
            lock (_lock)
            {
                return _key != null;
            }
        }
    }

    /// <inheritdoc />
    public event Action? VaultStateChanged;

    /// <inheritdoc />
    public void SetKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySizeInBytes)
        {
            throw new ArgumentException($"Key must be {KeySizeInBytes} bytes (256 bits).", nameof(key));
        }

        lock (_lock)
        {
            ClearKeyInternal();
            _key = (byte[])key.Clone();
        }

        VaultStateChanged?.Invoke();
    }

    /// <inheritdoc />
    public byte[] GetKey()
    {
        lock (_lock)
        {
            if (_key == null)
            {
                throw new InvalidOperationException("The vault is locked. No encryption key is available.");
            }

            return _key.ToArray();
        }
    }

    /// <inheritdoc />
    public void Lock()
    {
        lock (_lock)
        {
            ClearKeyInternal();
        }

        VaultStateChanged?.Invoke();
    }
    
    public void Dispose()
    {
        lock (_lock)
        {
            ClearKeyInternal();
        }
        VaultStateChanged = null;
    }

    private void ClearKeyInternal()
    {
        if (_key != null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }
}