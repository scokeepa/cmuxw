using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Media;
using Cmux.Core.Models;
using Cmux.Core.Config;
using Cmux.Core.Terminal;
using Cmux.Services;
using Cmux.ViewModels;
using Cmux.Views;

namespace Cmux.Controls;

/// <summary>
/// Recursively renders a SplitNode tree as nested Grid panels with
/// GridSplitters for resizable dividers. Leaf nodes contain TerminalControl instances.
/// </summary>
public class SplitPaneContainer : ContentControl
{
    private SurfaceViewModel? _surface;
    private readonly Dictionary<string, TerminalControl> _terminalCache = [];
    private readonly Dictionary<string, BrowserControl> _browserCache = [];

    public event Action? SearchRequested;

    private static SolidColorBrush GetThemeBrush(string key) =>
        Application.Current?.TryFindResource(key) as SolidColorBrush ?? Brushes.Transparent;

    private static Color GetThemeColor(string key)
    {
        if (Application.Current?.TryFindResource(key) is Color c)
            return c;

        if (string.Equals(key, "AccentColor", StringComparison.Ordinal)
            && Application.Current?.TryFindResource("AccentBrush") is SolidColorBrush ab)
            return ab.Color;

        return Colors.Transparent;
    }

    public SplitPaneContainer()
    {
        Background = Brushes.Transparent;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SurfaceViewModel oldSurface)
        {
            oldSurface.PropertyChanged -= OnSurfacePropertyChanged;
            oldSurface.PaneSessionReset -= OnPaneSessionReset;
        }

        // Clear terminal cache when switching surfaces/workspaces
        // This prevents reusing terminals from a different workspace
        _terminalCache.Clear();
        foreach (var paneId in _browserCache.Keys.ToList())
            BrowserPaneRegistry.Unregister(paneId);
        _browserCache.Clear();

        _surface = e.NewValue as SurfaceViewModel;

        if (_surface != null)
        {
            _surface.PropertyChanged += OnSurfacePropertyChanged;
            _surface.PaneSessionReset += OnPaneSessionReset;
            Rebuild();
        }
        else
        {
            Content = null;
        }
    }

    private void OnSurfacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SurfaceViewModel.RootNode)
            or nameof(SurfaceViewModel.IsZoomed))
        {
            Dispatcher.BeginInvoke(Rebuild);
        }
        else if (e.PropertyName is nameof(SurfaceViewModel.FocusedPaneId))
        {
            Dispatcher.BeginInvoke(UpdateFocusState);
        }
    }

    private void OnPaneSessionReset(string paneId)
    {
        _terminalCache.Remove(paneId);
        if (_browserCache.Remove(paneId, out _))
            BrowserPaneRegistry.Unregister(paneId);

        Dispatcher.BeginInvoke(Rebuild);
    }

    /// <summary>
    /// Updates only focus-related visual state on cached terminals without
    /// rebuilding the entire UI tree.
    /// </summary>
    private void UpdateFocusState()
    {
        if (_surface == null) return;

        if (_browserCache.Count > 0)
        {
            Rebuild();
            return;
        }

        // In zoom mode, focus change may require rebuild if the zoomed pane changed
        if (_surface.IsZoomed)
        {
            Rebuild();
            return;
        }

        foreach (var (paneId, terminal) in _terminalCache)
        {
            terminal.IsPaneFocused = paneId == _surface.FocusedPaneId;
        }
    }

    private void Rebuild()
    {
        if (_surface == null) return;

        // Zoom mode: show only the focused pane full-size
        if (_surface.IsZoomed && _surface.FocusedPaneId != null)
        {
            var focusedNode = _surface.RootNode.FindNode(_surface.FocusedPaneId);
            if (focusedNode != null)
            {
                Content = BuildLeaf(focusedNode);
                return;
            }
        }

        Content = BuildNode(_surface.RootNode);
    }

    private UIElement BuildNode(SplitNode node)
    {
        if (node.IsLeaf)
        {
            return BuildLeaf(node);
        }

        return BuildSplit(node);
    }

    private UIElement BuildLeaf(SplitNode node)
    {
        if (node.PaneId == null)
            return new Border { Background = Brushes.Transparent };

        var paneId = node.PaneId; // Capture for closures

        if (_surface?.IsBrowserPane(paneId) == true)
        {
            return BuildBrowserLeaf(paneId);
        }

        // Reuse cached terminal if available (preserves session and scroll position)
        if (!_terminalCache.TryGetValue(paneId, out var terminal))
        {
            terminal = new TerminalControl();
            _terminalCache[paneId] = terminal;
        }
        else
        {
            // Detach from old parent before reusing
            // Terminal could be inside DockPanel (with header) or Border
            var oldParent = System.Windows.Media.VisualTreeHelper.GetParent(terminal) as FrameworkElement;
            
            if (oldParent is DockPanel dockPanel)
            {
                dockPanel.Children.Remove(terminal);
            }
            else if (oldParent is Border border)
            {
                border.Child = null;
            }
            
            // Clear old event handlers to prevent memory leaks and wrong callbacks
            terminal.ClearEventHandlers();
        }

        // Wire up event handlers with closures capturing the current pane ID
        terminal.FocusRequested += () => _surface?.FocusPane(paneId);
        terminal.CommandInterceptRequested += command => _surface?.TryHandlePaneCommand(paneId, command) == true;
        terminal.CommandSubmitted += command => _surface?.RegisterCommandSubmission(paneId, command);
        terminal.ClearRequested += () => _surface?.CapturePaneTranscript(paneId, "clear-terminal");
        terminal.SplitRequested += dir =>
        {
            _surface?.FocusPane(paneId);
            _surface?.SplitFocused(dir);
        };
        terminal.ZoomRequested += () => _surface?.ToggleZoom();
        terminal.ClosePaneRequested += () => _surface?.ClosePane(paneId);
        terminal.SearchRequested += () => SearchRequested?.Invoke();
        terminal.IsPaneFocused = paneId == _surface?.FocusedPaneId;
        terminal.IsSurfaceZoomed = _surface?.IsZoomed == true;

        // Attach the terminal session
        var session = _surface?.GetSession(paneId);
        if (session != null)
            terminal.AttachSession(session);

        // Get pane title (custom name takes precedence over shell title)
        var title = _surface?.GetPaneTitle(paneId, session?.Title) ?? L.T("Terminal");

        // Create panel with header
        var panel = new DockPanel { LastChildFill = true };

        // Header bar with title and close button
        var header = new Border
        {
            Background = GetThemeBrush("SidebarItemHoverBrush"),
            Height = 22,
            Padding = new Thickness(8, 2, 8, 2),
        };

        var headerMenu = new ContextMenu();
        var renamePane = new MenuItem { Header = L.T("Rename Pane") };
        renamePane.Click += (_, _) =>
        {
            var currentName = _surface?.GetPaneTitle(paneId, session?.Title) ?? L.T("Terminal");
            var prompt = new TextPromptWindow(
                title: L.T("Rename Pane"),
                message: L.T("Set a custom name for this pane."),
                defaultValue: currentName)
            {
                Owner = Window.GetWindow(this),
            };

            if (prompt.ShowDialog() == true && !string.IsNullOrWhiteSpace(prompt.ResponseText))
                _surface?.SetPaneCustomName(paneId, prompt.ResponseText);
        };
        headerMenu.Items.Add(renamePane);

        var resetPaneName = new MenuItem { Header = L.T("Reset Pane Name") };
        resetPaneName.Click += (_, _) => _surface?.SetPaneCustomName(paneId, string.Empty);
        headerMenu.Items.Add(resetPaneName);

        header.ContextMenu = headerMenu;

        DockPanel.SetDock(header, Dock.Top);

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }); // Focus indicator
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }); // Reset
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }); // Close

        // Focus indicator (shows which pane is focused)
        var focusIndicator = new Border
        {
            Width = 3,
            Height = 12,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = terminal.IsPaneFocused
                ? GetThemeBrush("AccentBrush")
                : GetThemeBrush("DividerBrush"),
        };
        Grid.SetColumn(focusIndicator, 0);

        // Title text
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 11,
            Foreground = GetThemeBrush("ForegroundBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(titleText, 1);

        var resetButton = new Button
        {
            Content = "\uE149",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10,
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            Foreground = GetThemeBrush("ForegroundDimBrush"),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = L.T("Reset pane session"),
        };
        resetButton.Click += (_, _) => _surface?.ResetPaneSession(paneId);
        Grid.SetColumn(resetButton, 2);

        // Close button
        var closeButton = new Button
        {
            Content = "\u2715",
            FontSize = 10,
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            Foreground = GetThemeBrush("ForegroundDimBrush"),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = L.T("Close pane"),
        };
        closeButton.Click += (s, e) => _surface?.ClosePane(paneId);
        Grid.SetColumn(closeButton, 3);

        headerGrid.Children.Add(focusIndicator);
        headerGrid.Children.Add(titleText);
        headerGrid.Children.Add(resetButton);
        headerGrid.Children.Add(closeButton);
        header.Child = headerGrid;

        panel.Children.Add(header);
        panel.Children.Add(terminal);

        var focusedAccent = GetThemeColor("AccentColor");
        return new Border
        {
            Background = GetThemeBrush("TerminalHostBackgroundBrush"),
            Child = panel,
            BorderBrush = terminal.IsPaneFocused
                ? new SolidColorBrush(Color.FromArgb(153, focusedAccent.R, focusedAccent.G, focusedAccent.B))
                : GetThemeBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
        };
    }

    private UIElement BuildBrowserLeaf(string paneId)
    {
        if (_surface == null)
            return new Border { Background = Brushes.Transparent };

        if (!_browserCache.TryGetValue(paneId, out var browser))
        {
            browser = new BrowserControl();
            _browserCache[paneId] = browser;
        }
        else
        {
            var oldParent = VisualTreeHelper.GetParent(browser) as FrameworkElement;
            if (oldParent is DockPanel dockPanel)
                dockPanel.Children.Remove(browser);
            else if (oldParent is Border border)
                border.Child = null;

            browser.ClearEventHandlers();
        }

        browser.FocusRequested += () => _surface.FocusPane(paneId);
        browser.CloseRequested += () => _surface.ClosePane(paneId);
        BrowserPaneRegistry.Register(paneId, browser);

        var targetUrl = _surface.GetBrowserPaneUrl(paneId);
        if (!string.IsNullOrWhiteSpace(targetUrl) &&
            !string.Equals(browser.GetCurrentUrl(), targetUrl, StringComparison.OrdinalIgnoreCase))
        {
            browser.Navigate(targetUrl);
        }

        var title = _surface.GetPaneTitle(paneId, "Browser");
        if (string.Equals(title, L.T("Terminal"), StringComparison.Ordinal))
            title = "Browser";

        var panel = new DockPanel { LastChildFill = true };
        var header = new Border
        {
            Background = GetThemeBrush("SidebarItemHoverBrush"),
            Height = 22,
            Padding = new Thickness(8, 2, 8, 2),
        };

        DockPanel.SetDock(header, Dock.Top);
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var focusIndicator = new Border
        {
            Width = 3,
            Height = 12,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = paneId == _surface.FocusedPaneId
                ? GetThemeBrush("AccentBrush")
                : GetThemeBrush("DividerBrush"),
        };
        Grid.SetColumn(focusIndicator, 0);

        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 11,
            Foreground = GetThemeBrush("ForegroundBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(titleText, 1);

        var resetBrowserButton = new Button
        {
            Content = "\uE149",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10,
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            Foreground = GetThemeBrush("ForegroundDimBrush"),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = L.T("Reset pane session"),
        };
        resetBrowserButton.Click += (_, _) => _surface.ResetPaneSession(paneId);
        Grid.SetColumn(resetBrowserButton, 2);

        var closeButton = new Button
        {
            Content = "\u2715",
            FontSize = 10,
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            Foreground = GetThemeBrush("ForegroundDimBrush"),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = L.T("Close pane"),
        };
        closeButton.Click += (_, _) => _surface.ClosePane(paneId);
        Grid.SetColumn(closeButton, 3);

        headerGrid.Children.Add(focusIndicator);
        headerGrid.Children.Add(titleText);
        headerGrid.Children.Add(resetBrowserButton);
        headerGrid.Children.Add(closeButton);
        header.Child = headerGrid;

        panel.Children.Add(header);
        panel.Children.Add(browser);

        var focusedAccent = GetThemeColor("AccentColor");
        return new Border
        {
            Background = GetThemeBrush("TerminalHostBackgroundBrush"),
            Child = panel,
            BorderBrush = paneId == _surface.FocusedPaneId
                ? new SolidColorBrush(Color.FromArgb(153, focusedAccent.R, focusedAccent.G, focusedAccent.B))
                : GetThemeBrush("BorderBrush"),
            BorderThickness = new Thickness(1),
        };
    }


    private UIElement BuildSplit(SplitNode node)
    {
        var grid = new Grid();

        if (node.Direction == SplitDirection.Vertical)
        {
            // Left | Right
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(node.SplitRatio, GridUnitType.Star),
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(4, GridUnitType.Pixel),
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1 - node.SplitRatio, GridUnitType.Star),
            });

            if (node.First != null)
            {
                var first = BuildNode(node.First);
                Grid.SetColumn(first, 0);
                grid.Children.Add(first);
            }

            var splitter = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Application.Current?.TryFindResource("DividerBrush") as Brush ?? Brushes.Gray,
                Cursor = System.Windows.Input.Cursors.SizeWE,
            };
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);

            if (node.Second != null)
            {
                var second = BuildNode(node.Second);
                Grid.SetColumn(second, 2);
                grid.Children.Add(second);
            }
        }
        else
        {
            // Top / Bottom
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(node.SplitRatio, GridUnitType.Star),
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(4, GridUnitType.Pixel),
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1 - node.SplitRatio, GridUnitType.Star),
            });

            if (node.First != null)
            {
                var first = BuildNode(node.First);
                Grid.SetRow(first, 0);
                grid.Children.Add(first);
            }

            var splitter = new GridSplitter
            {
                Height = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Application.Current?.TryFindResource("DividerBrush") as Brush ?? Brushes.Gray,
                Cursor = System.Windows.Input.Cursors.SizeNS,
            };
            Grid.SetRow(splitter, 1);
            grid.Children.Add(splitter);

            if (node.Second != null)
            {
                var second = BuildNode(node.Second);
                Grid.SetRow(second, 2);
                grid.Children.Add(second);
            }
        }

        return grid;
    }

    /// <summary>
    /// Updates settings for all cached terminal controls.
    /// </summary>
    public void UpdateAllTerminals(TerminalTheme theme, string fontFamily, int fontSize)
    {
        foreach (var terminal in _terminalCache.Values)
        {
            terminal.UpdateSettings(theme, fontFamily, fontSize);
        }
    }

    /// <summary>
    /// Moves keyboard focus to the currently focused pane terminal.
    /// Returns true if focus was moved.
    /// </summary>
    public bool FocusActivePane()
    {
        if (_surface == null)
            return false;

        var paneId = _surface.FocusedPaneId;
        if (string.IsNullOrWhiteSpace(paneId))
        {
            paneId = _surface.RootNode.GetLeaves()
                .Select(l => l.PaneId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        if (string.IsNullOrWhiteSpace(paneId))
            return false;

        if (!_terminalCache.TryGetValue(paneId, out var terminal))
        {
            if (_browserCache.TryGetValue(paneId, out var browser))
            {
                browser.Focus();
                Keyboard.Focus(browser);
                return true;
            }

            Rebuild();
            if (!_terminalCache.TryGetValue(paneId, out terminal))
            {
                if (_browserCache.TryGetValue(paneId, out var rebuiltBrowser))
                {
                    rebuiltBrowser.Focus();
                    Keyboard.Focus(rebuiltBrowser);
                    return true;
                }

                return false;
            }
        }

        terminal.Focus();
        Keyboard.Focus(terminal);
        return true;
    }

    /// <summary>
    /// Rebuilds pane chrome so borders/headers created in code pick up new theme brushes
    /// (resource dictionary may replace frozen brushes with new instances).
    /// </summary>
    public void RefreshChromeForTheme()
    {
        if (_surface == null)
            return;

        Dispatcher.BeginInvoke(new Action(Rebuild), DispatcherPriority.Background);
    }
}
