using System.Security.Cryptography;
using System.Text;

namespace MyPass.Core.Security;

/// <summary>
/// Provides cryptographic operations for key derivation, verification, and authenticated encryption.
/// </summary>
public interface ICryptoService
{
    /// <summary>
    /// Generates cryptographically strong random bytes.
    /// </summary>
    /// <param name="size">Number of bytes to generate (default 32).</param>
    /// <returns>Cryptographically secure random byte array.</returns>
    byte[] GenerateSalt(int size = 32);

    /// <summary>
    /// Derives a 32-byte (256-bit) encryption key from the given master password and salt using PBKDF2 (SHA-256).
    /// </summary>
    /// <param name="masterPassword">The master password.</param>
    /// <param name="salt">Cryptographically random salt.</param>
    /// <param name="iterations">Number of PBKDF2 iterations (default 600,000).</param>
    /// <returns>A 32-byte derived key.</returns>
    byte[] DeriveKey(string masterPassword, byte[] salt, int iterations = 600_000);

    /// <summary>
    /// Computes a SHA-256 hash of the derived key and returns it as a Base64 string for verification.
    /// </summary>
    /// <param name="key">The derived 32-byte key.</param>
    /// <returns>Base64-encoded SHA-256 hash.</returns>
    string CreateVerifier(byte[] key);

    /// <summary>
    /// Verifies that a derived key matches the expected verifier hash using a constant-time comparison.
    /// </summary>
    /// <param name="key">The derived key to test.</param>
    /// <param name="expectedVerifierBase64">The stored Base64 verifier hash.</param>
    /// <returns><c>true</c> if the key matches the verifier; otherwise, <c>false</c>.</returns>
    bool VerifyKey(byte[] key, string expectedVerifierBase64);

    /// <summary>
    /// Encrypts UTF-8 plaintext using AES-GCM (256-bit) with a fresh 12-byte nonce and 16-byte authentication tag.
    /// Combines [Nonce (12B)] + [Tag (16B)] + [Ciphertext] into a single Base64 string.
    /// </summary>
    /// <param name="plainText">The plaintext string to encrypt.</param>
    /// <param name="key">The 32-byte encryption key.</param>
    /// <returns>Base64-encoded payload containing Nonce, Tag, and Ciphertext.</returns>
    string Encrypt(string plainText, byte[] key);

    /// <summary>
    /// Decrypts a Base64 payload formatted as [Nonce (12B)] + [Tag (16B)] + [Ciphertext] using AES-GCM and returns the UTF-8 string.
    /// </summary>
    /// <param name="encryptedPayloadBase64">Base64-encoded payload.</param>
    /// <param name="key">The 32-byte encryption key.</param>
    /// <returns>Decrypted plaintext string.</returns>
    string Decrypt(string encryptedPayloadBase64, byte[] key);
}

/// <summary>
/// Implements cryptographic operations using standard .NET System.Security.Cryptography algorithms.
/// </summary>
public class CryptoService : ICryptoService
{
    private const int KeySizeInBytes = 32;
    private const int NonceSizeInBytes = 12; // AesGcm.NonceByteSizes.MaxSize
    private const int TagSizeInBytes = 16;   // AesGcm.TagByteSizes.MaxSize

    /// <inheritdoc />
    public byte[] GenerateSalt(int size = 32)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return RandomNumberGenerator.GetBytes(size);
    }

    /// <inheritdoc />
    public byte[] DeriveKey(string masterPassword, byte[] salt, int iterations = 600_000)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        return Rfc2898DeriveBytes.Pbkdf2(
            masterPassword,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySizeInBytes);
    }

    /// <inheritdoc />
    public string CreateVerifier(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        byte[] hash = SHA256.HashData(key);
        return Convert.ToBase64String(hash);
    }

    /// <inheritdoc />
    public bool VerifyKey(byte[] key, string expectedVerifierBase64)
    {
        if (key == null || string.IsNullOrWhiteSpace(expectedVerifierBase64))
        {
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromBase64String(expectedVerifierBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualHash = SHA256.HashData(key);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    /// <inheritdoc />
    public string Encrypt(string plainText, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(plainText);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySizeInBytes)
        {
            throw new ArgumentException($"Key must be {KeySizeInBytes} bytes (256 bits).", nameof(key));
        }

        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] payload = new byte[NonceSizeInBytes + TagSizeInBytes + plainTextBytes.Length];

        Span<byte> nonce = payload.AsSpan(0, NonceSizeInBytes);
        Span<byte> tag = payload.AsSpan(NonceSizeInBytes, TagSizeInBytes);
        Span<byte> cipherText = payload.AsSpan(NonceSizeInBytes + TagSizeInBytes);

        RandomNumberGenerator.Fill(nonce);

        try
        {
            using var aesGcm = new AesGcm(key, TagSizeInBytes);
            aesGcm.Encrypt(nonce, plainTextBytes, cipherText, tag);
            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainTextBytes);
        }
    }

    /// <inheritdoc />
    public string Decrypt(string encryptedPayloadBase64, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(encryptedPayloadBase64);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != KeySizeInBytes)
        {
            throw new ArgumentException($"Key must be {KeySizeInBytes} bytes (256 bits).", nameof(key));
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(encryptedPayloadBase64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("The encrypted payload is not a valid Base64 string.", ex);
        }

        int minPayloadLength = NonceSizeInBytes + TagSizeInBytes;
        if (payload.Length < minPayloadLength)
        {
            throw new CryptographicException("The encrypted payload is invalid or corrupted.");
        }

        ReadOnlySpan<byte> nonce = payload.AsSpan(0, NonceSizeInBytes);
        ReadOnlySpan<byte> tag = payload.AsSpan(NonceSizeInBytes, TagSizeInBytes);
        ReadOnlySpan<byte> cipherText = payload.AsSpan(minPayloadLength);

        byte[] plainTextBytes = new byte[cipherText.Length];
        try
        {
            using var aesGcm = new AesGcm(key, TagSizeInBytes);
            aesGcm.Decrypt(nonce, cipherText, tag, plainTextBytes);
            return Encoding.UTF8.GetString(plainTextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainTextBytes);
        }
    }
}
