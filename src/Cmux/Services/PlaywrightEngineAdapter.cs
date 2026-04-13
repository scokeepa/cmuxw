using Cmux.Controls;

namespace Cmux.Services;

/// <summary>
/// Phase 2 automation adapter surface compatible with Playwright-style operations.
/// The current implementation executes actions against the active WebView2 pane,
/// while keeping a stable adapter contract for a future Playwright backend swap.
/// </summary>
internal sealed class PlaywrightEngineAdapter
{
    public static PlaywrightEngineAdapter Instance { get; } = new();

    private PlaywrightEngineAdapter() { }

    public Task<string> SnapshotAsync(BrowserControl browser)
    {
        return browser.GetAccessibilitySnapshot();
    }

    public Task ClickAsync(BrowserControl browser, string selector)
    {
        return browser.ClickElement(selector);
    }

    public Task FillAsync(BrowserControl browser, string selector, string value)
    {
        return browser.FillElement(selector, value);
    }

    public Task TypeAsync(BrowserControl browser, string selector, string value)
    {
        return browser.TypeElement(selector, value);
    }

    public Task<string> EvalAsync(BrowserControl browser, string script)
    {
        return browser.EvaluateJavaScript(script);
    }
}
