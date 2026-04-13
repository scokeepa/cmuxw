using System.Collections.Concurrent;
using Cmux.Controls;

namespace Cmux.Services;

internal static class BrowserPaneRegistry
{
    private static readonly ConcurrentDictionary<string, WeakReference<BrowserControl>> _panes = new(StringComparer.Ordinal);

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
}
