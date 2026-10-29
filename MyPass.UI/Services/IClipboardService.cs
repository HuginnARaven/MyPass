namespace MyPass.UI.Services;

/// <summary>
/// Provides secure clipboard operations with automatic time-based clearing.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies text to the Windows Clipboard and clears it after the specified duration if it has not changed.
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    /// <param name="clearAfterSeconds">The number of seconds before clearing the clipboard (default 15).</param>
    void CopyWithAutoClear(string text, int clearAfterSeconds = 15);
}
