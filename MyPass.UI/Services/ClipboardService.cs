using System.Runtime.InteropServices;
using System.Windows;

namespace MyPass.UI.Services;

/// <summary>
/// Implements secure clipboard operations with cancellation-aware auto-clearing
/// and retry logic to handle transient <c>COMException (0x800401D0 CLIPBRD_E_CANT_OPEN)</c>
/// errors that occur when Clipboard History or another process briefly holds the clipboard lock.
/// </summary>
public class ClipboardService : IClipboardService, IDisposable
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly object _lock = new();
    private CancellationTokenSource? _clearCts;

    /// <summary>
    /// Cancels any running auto-clear timer on shutdown.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _clearCts?.Cancel();
            _clearCts?.Dispose();
            _clearCts = null;
        }
    }

    /// <inheritdoc />
    public void CopyWithAutoClear(string text, int clearAfterSeconds = 15)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        CancellationToken token;
        lock (_lock)
        {
            _clearCts?.Cancel();
            _clearCts?.Dispose();
            _clearCts = new CancellationTokenSource();
            token = _clearCts.Token;
        }

        // Copy text to the Windows Clipboard on the UI thread, with retry.
        ExecuteOnUi(() => SetClipboardWithRetry(text));

        // Background timer to auto-clear the clipboard after the specified delay.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(clearAfterSeconds), token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                ExecuteOnUi(() =>
                {
                    try
                    {
                        // Only clear if our text is still on the clipboard — do not wipe
                        // content that the user or another application has placed there since.
                        if (Clipboard.ContainsText() && Clipboard.GetText() == text)
                        {
                            ClearClipboardWithRetry();
                        }
                    }
                    catch
                    {
                        // Ignore all clipboard access issues during auto-clear.
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Expected when a new copy operation cancels the previous timer.
            }
        }, token);
    }

    /// <summary>
    /// Attempts to set clipboard text up to <see cref="MaxRetries"/> times with a short
    /// delay between attempts to handle transient clipboard locking by other processes.
    /// Uses <c>SetDataObject(data, true)</c> so the data persists after the application exits.
    /// </summary>
    private static void SetClipboardWithRetry(string text)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                // SetDataObject with copy=true is more reliable than SetText
                // because it uses a lower-level OLE call and the bool ensures
                // the data is flushed to the clipboard manager even after app exit.
                Clipboard.SetDataObject(text, true);
                return;
            }
            catch (COMException)
            {
                if (attempt == MaxRetries - 1)
                {
                    // All retries exhausted — give up silently; the clipboard state is unknown.
                    return;
                }

                Thread.Sleep(RetryDelay);
            }
            catch
            {
                // Non-COM exceptions (e.g. InvalidOperationException from non-STA thread) —
                // surface nothing, the clipboard simply won't be set.
                return;
            }
        }
    }

    /// <summary>
    /// Attempts to clear the clipboard up to <see cref="MaxRetries"/> times.
    /// </summary>
    private static void ClearClipboardWithRetry()
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                Clipboard.Clear();
                return;
            }
            catch (COMException)
            {
                if (attempt == MaxRetries - 1)
                {
                    return;
                }

                Thread.Sleep(RetryDelay);
            }
            catch
            {
                return;
            }
        }
    }

    private static void ExecuteOnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
