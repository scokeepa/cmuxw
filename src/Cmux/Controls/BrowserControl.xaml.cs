using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using System.IO;

namespace Cmux.Controls;

/// <summary>
/// In-app browser control using WebView2. Provides a toolbar (back/forward/reload/address bar)
/// and a scriptable API for agents to interact with web pages.
/// </summary>
public partial class BrowserControl : UserControl
{
    private static readonly Lazy<Task<CoreWebView2Environment>> SharedEnvironmentTask = new(() =>
        CoreWebView2Environment.CreateAsync());

    private string? _pendingNavigateUrl;
    private bool _isWebViewReady;

    public event Action? CloseRequested;
    public event Action? FocusRequested;

    public BrowserControl()
    {
        InitializeComponent();
        PreviewMouseDown += (_, _) => FocusRequested?.Invoke();
        InitializeWebView();
    }

    public static void WarmUp()
    {
        _ = WarmUpSafeAsync();
    }

    private static async Task WarmUpSafeAsync()
    {
        try
        {
            await SharedEnvironmentTask.Value;
        }
        catch
        {
            // Ignore warm-up failures; regular initialization path reports details.
        }
    }

    public void ClearEventHandlers()
    {
        CloseRequested = null;
        FocusRequested = null;
    }

    private async void InitializeWebView()
    {
        try
        {
            var env = await SharedEnvironmentTask.Value;
            await WebView.EnsureCoreWebView2Async(env);
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            _isWebViewReady = true;

            if (!string.IsNullOrWhiteSpace(_pendingNavigateUrl))
            {
                var url = _pendingNavigateUrl;
                _pendingNavigateUrl = null;
                Navigate(url);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
        }
    }

    /// <summary>Navigate to a URL.</summary>
    public void Navigate(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        _pendingNavigateUrl = url;
        AddressBar.Text = url;

        if (!_isWebViewReady || WebView.CoreWebView2 == null)
            return;

        try
        {
            WebView.CoreWebView2.Navigate(url);
            _pendingNavigateUrl = null;
        }
        catch
        {
            // Invalid URL
        }
    }

    /// <summary>Execute JavaScript and return the result.</summary>
    public async Task<string> EvaluateJavaScript(string script)
    {
        if (WebView.CoreWebView2 == null) return "";
        return await WebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    public async Task CaptureScreenshotAsync(string path)
    {
        if (WebView.CoreWebView2 == null)
            throw new InvalidOperationException("Browser control is not ready.");

        await using var fs = File.Create(path);
        await WebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
    }

    /// <summary>Get the accessibility tree snapshot (simplified).</summary>
    public async Task<string> GetAccessibilitySnapshot()
    {
        const string script = @"
            (function() {
                function walk(node) {
                    const result = {
                        role: node.getAttribute('role') || node.tagName.toLowerCase(),
                        name: node.getAttribute('aria-label') || node.textContent?.substring(0, 100) || '',
                        children: []
                    };
                    for (const child of node.children) {
                        result.children.push(walk(child));
                    }
                    return result;
                }
                return JSON.stringify(walk(document.body));
            })()
        ";
        return await EvaluateJavaScript(script);
    }

    /// <summary>Click an element by CSS selector.</summary>
    public async Task ClickElement(string selector)
    {
        var escapedSelector = selector.Replace("'", "\\'");
        await EvaluateJavaScript($"document.querySelector('{escapedSelector}')?.click()");
    }

    /// <summary>Fill a form field by CSS selector.</summary>
    public async Task FillElement(string selector, string value)
    {
        var escapedSelector = selector.Replace("'", "\\'");
        var escapedValue = value.Replace("'", "\\'");
        await EvaluateJavaScript($@"
            (() => {{
                const el = document.querySelector('{escapedSelector}');
                if (el) {{
                    el.value = '{escapedValue}';
                    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                }}
            }})()
        ");
    }

    /// <summary>Type text into an element by CSS selector.</summary>
    public async Task TypeElement(string selector, string value)
    {
        var escapedSelector = selector.Replace("'", "\\'");
        var escapedValue = value.Replace("'", "\\'");
        await EvaluateJavaScript($@"
            (() => {{
                const el = document.querySelector('{escapedSelector}');
                if (el) {{
                    if (typeof el.focus === 'function') el.focus();
                    if ('value' in el) {{
                        el.value = (el.value || '') + '{escapedValue}';
                        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    }} else {{
                        el.textContent = (el.textContent || '') + '{escapedValue}';
                    }}
                }}
            }})()
        ");
    }

    /// <summary>Get the current page URL.</summary>
    public string GetCurrentUrl()
    {
        return WebView.CoreWebView2?.Source ?? "";
    }

    // Event handlers

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoBack == true)
            WebView.CoreWebView2.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2?.CanGoForward == true)
            WebView.CoreWebView2.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        WebView.CoreWebView2?.Reload();
    }

    private void CloseBrowser_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Navigate(AddressBar.Text);
            e.Handled = true;
        }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        AddressBar.Text = WebView.CoreWebView2?.Source ?? "";
    }

    private void WebView_SourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        AddressBar.Text = WebView.CoreWebView2?.Source ?? "";
    }
}
