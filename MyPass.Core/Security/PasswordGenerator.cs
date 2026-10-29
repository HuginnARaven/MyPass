using System.Security.Cryptography;

namespace MyPass.Core.Security;

/// <summary>
/// Provides cryptographically secure password generation functionality.
/// </summary>
public interface IPasswordGenerator
{
    /// <summary>
    /// Generates a cryptographically secure random password with the specified length and character sets.
    /// Guarantees that at least one character from each enabled set is included.
    /// </summary>
    /// <param name="length">The length of the password (default 16).</param>
    /// <param name="includeUppercase">Whether to include uppercase letters (A-Z).</param>
    /// <param name="includeLowercase">Whether to include lowercase letters (a-z).</param>
    /// <param name="includeDigits">Whether to include digits (0-9).</param>
    /// <param name="includeSymbols">Whether to include special symbols.</param>
    /// <returns>A random password string.</returns>
    string GeneratePassword(
        int length = 16,
        bool includeUppercase = true,
        bool includeLowercase = true,
        bool includeDigits = true,
        bool includeSymbols = true);
}

/// <summary>
/// Implements cryptographically secure password generation using <see cref="RandomNumberGenerator"/>.
/// </summary>
public class PasswordGenerator : IPasswordGenerator
{
    public const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    public const string DigitChars = "0123456789";
    public const string SymbolChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";

    /// <inheritdoc />
    public string GeneratePassword(
        int length = 16,
        bool includeUppercase = true,
        bool includeLowercase = true,
        bool includeDigits = true,
        bool includeSymbols = true)
    {
        int requiredSetsCount = (includeUppercase ? 1 : 0) +
                                (includeLowercase ? 1 : 0) +
                                (includeDigits ? 1 : 0) +
                                (includeSymbols ? 1 : 0);

        if (requiredSetsCount == 0)
        {
            throw new ArgumentException("At least one character set must be selected.");
        }

        if (length < requiredSetsCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                $"Password length ({length}) must be at least the number of selected character sets ({requiredSetsCount}).");
        }

        char[] passwordChars = new char[length];
        int currentIndex = 0;

        // Ensure at least one character from each enabled set is included
        if (includeUppercase)
        {
            passwordChars[currentIndex++] = UppercaseChars[RandomNumberGenerator.GetInt32(UppercaseChars.Length)];
        }

        if (includeLowercase)
        {
            passwordChars[currentIndex++] = LowercaseChars[RandomNumberGenerator.GetInt32(LowercaseChars.Length)];
        }

        if (includeDigits)
        {
            passwordChars[currentIndex++] = DigitChars[RandomNumberGenerator.GetInt32(DigitChars.Length)];
        }

        if (includeSymbols)
        {
            passwordChars[currentIndex++] = SymbolChars[RandomNumberGenerator.GetInt32(SymbolChars.Length)];
        }

        // Build combined pool of all enabled character sets
        string pool = string.Concat(
            includeUppercase ? UppercaseChars : string.Empty,
            includeLowercase ? LowercaseChars : string.Empty,
            includeDigits ? DigitChars : string.Empty,
            includeSymbols ? SymbolChars : string.Empty);

        // Fill remaining positions from combined pool
        while (currentIndex < length)
        {
            passwordChars[currentIndex++] = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        }

        // Cryptographically secure Fisher-Yates shuffle
        for (int i = passwordChars.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (passwordChars[i], passwordChars[j]) = (passwordChars[j], passwordChars[i]);
        }

        return new string(passwordChars);
    }
}
