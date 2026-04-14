using System.Collections.Concurrent;
using System.Threading;
using Cmux.Controls;

namespace Cmux.Services;

internal static class BrowserPaneRegistry
{
    private static readonly ConcurrentDictionary<string, WeakReference<BrowserControl>> _panes = new(StringComparer.Ordinal);
    private static int _webViewAirspaceSuppressDepth;
    public static void Register(string paneId, BrowserControl control)
    {
        if (string.IsNullOrWhiteSpace(paneId))
            return;

        _panes[paneId] = new WeakReference<BrowserControl>(control);
    }

    public static void Unregister(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
            return;

        _panes.TryRemove(paneId, out _);
    }

    public static BrowserControl? Get(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
            return null;

        if (!_panes.TryGetValue(paneId, out var reference))
            return null;

        if (reference.TryGetTarget(out var control))
            return control;

        _panes.TryRemove(paneId, out _);
        return null;
    }

    /// <summary>
    /// Increments overlay depth; when the first overlay opens, all WebView2 instances are hidden (HWND airspace fix).
    /// </summary>
    public static void PushWebViewAirspaceSuppress()
    {
        if (Interlocked.Increment(ref _webViewAirspaceSuppressDepth) != 1)
            return;

        ApplyWebViewAirspaceSuppressed(true);
    }

    /// <summary>
    /// Decrements overlay depth; when the last overlay closes, WebView2 controls are shown again.
    /// </summary>
    public static void PopWebViewAirspaceSuppress()
    {
        var newDepth = Interlocked.Decrement(ref _webViewAirspaceSuppressDepth);
        if (newDepth < 0)
        {
            Interlocked.Exchange(ref _webViewAirspaceSuppressDepth, 0);
            return;
        }

        if (newDepth != 0)
            return;

        ApplyWebViewAirspaceSuppressed(false);
    }

    private static void ApplyWebViewAirspaceSuppressed(bool suppress)
    {
        foreach (var reference in _panes.Values)
        {
            if (reference.TryGetTarget(out var browser))
                browser.SetWebViewAirspaceSuppressed(suppress);
        }
    }
}