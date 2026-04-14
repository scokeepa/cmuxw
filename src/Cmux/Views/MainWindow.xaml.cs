using System.Windows;
using System;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using Cmux.Controls;
using Cmux.Core.Services;
using Cmux.ViewModels;
using Cmux.Services;

namespace Cmux.Views;

public partial class MainWindow : Window
{
    private const string FallbackBrowserUrl = "https://github.com/scokeepa/cmuxw/blob/master/docs/USER_GUIDE.md";
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private readonly DispatcherTimer _uiRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private ICollectionView? _workspaceView;
    private readonly Dictionary<string, AgentChatMessageView> _streamingAssistantByThread = new(StringComparer.Ordinal);
    private List<AgentThreadView> _agentThreadViews = [];
    private List<AgentChatMessageView> _allThreadMessages = [];
    private readonly ObservableCollection<AgentChatMessageView> _visibleThreadMessages = [];
    private string? _selectedAgentThreadId;
    private string _lastAgentContextKey = "";

    public MainWindow()
    {
        InitializeComponent();
        WindowAppearance.Apply(this);
        SetupWorkspaceFilter();

        CommandPaletteControl.PaletteClosed += () => FocusTerminal();
        CommandPaletteControl.ItemExecuted += item => FocusTerminal();
        NotificationPanelControl.NotificationClicked += n => ViewModel.NavigateToNotification(n);


        // Wire snippet picker events
        SnippetPickerControl.SnippetSelected += OnSnippetSelected;
        SnippetPickerControl.Closed += () =>
        {
            if (SnippetPickerControl.Visibility == Visibility.Visible)
                BrowserPaneRegistry.PopWebViewAirspaceSuppress();
            SnippetPickerControl.Visibility = Visibility.Collapsed;
        };

        // Wire inline search events from tab bar
        SurfaceTabBarControl.SearchTextChanged += OnSearchTextChanged;
        SurfaceTabBarControl.NextMatchRequested += OnSearchNext;
        SurfaceTabBarControl.PreviousMatchRequested += OnSearchPrevious;

        // Wire terminal surface events
        SplitPaneContainerControl.SearchRequested += () =>
        {
            SurfaceTabBarControl.FocusSearch();
        };

        // Periodically refresh lightweight UI state (pane count, zoom icon)
        _uiRefreshTimer.Tick += (_, _) => RefreshSurfaceUiState();
        _uiRefreshTimer.Start();

        // Subscribe to settings changes
        Cmux.Core.Config.SettingsService.SettingsChanged += OnSettingsChanged;
        OnSettingsChanged();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        App.AgentRuntime.RuntimeUpdated += OnAgentRuntimeUpdated;
        App.AgentConversationStore.StoreChanged += OnAgentConversationStoreChanged;
        AgentMessagesList.ItemsSource = _visibleThreadMessages;
        UpdateAgentPanelLayout();
        RefreshAgentThreads();
    }

    private void OnSettingsChanged()
    {
        var settings = Cmux.Core.Config.SettingsService.Current;
        var theme = Cmux.Core.Config.TerminalThemes.GetEffective(settings);

        Opacity = Math.Clamp(settings.Opacity, 0.5, 1.0);

        SplitPaneContainerControl.RefreshChromeForTheme();

        // Update all visible terminal controls
        foreach (var workspace in ViewModel.Workspaces)
        {
            foreach (var surface in workspace.Surfaces)
            {
                // Find the SplitPaneContainer for this surface and update terminals
                var container = FindVisualChild<SplitPaneContainer>(ContentArea, null);
                if (container != null)
                {
                    container.UpdateAllTerminals(theme, settings.FontFamily, settings.FontSize);
                }
            }
        }

        ApplyAgentChatFont(settings);
        UpdateThemeToggleButton(settings.ThemeName);
        RefreshSurfaceUiState();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = Cmux.Core.Config.SettingsService.Current;
        var isLight = IsLightTheme(settings.ThemeName);
        settings.ThemeName = isLight ? "Default Dark" : "Default Light";
        Cmux.Core.Config.SettingsService.Save();
        Cmux.Core.Config.SettingsService.NotifyChanged();
    }

    private void UpdateThemeToggleButton(string? themeName)
    {
        var isLight = IsLightTheme(themeName);
        ThemeToggleIcon.Text = isLight ? "\uE708" : "\uE706";
        ThemeToggleButton.ToolTip = isLight ? L.T("Switch to Dark") : L.T("Switch to Light");
    }

    private static bool IsLightTheme(string? themeName)
    {
        var theme = (themeName ?? "").Trim();
        if (theme.Equals("Default Light", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
            return true;

        if (theme.Equals("System", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("System Follow", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Follow System", StringComparison.OrdinalIgnoreCase))
            return IsSystemLightTheme();

        return false;
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyAgentChatFont(Cmux.Core.Config.CmuxSettings settings)
    {
        var agent = settings.Agent ?? new Cmux.Core.Config.AgentSettings();
        var fontFamilyName = string.IsNullOrWhiteSpace(agent.ChatFontFamily)
            ? settings.FontFamily
            : agent.ChatFontFamily.Trim();
        var fontSize = Math.Clamp(agent.ChatFontSize, 9, 28);

        System.Windows.Media.FontFamily fontFamily;
        try
        {
            fontFamily = new System.Windows.Media.FontFamily(fontFamilyName);
        }
        catch
        {
            fontFamily = new System.Windows.Media.FontFamily("Cascadia Code");
        }

        AgentThreadsList.FontFamily = fontFamily;
        AgentThreadsList.FontSize = fontSize;
        AgentMessagesList.FontFamily = fontFamily;
        AgentMessagesList.FontSize = fontSize;
        AgentPromptBox.FontFamily = fontFamily;
        AgentPromptBox.FontSize = fontSize;
        AgentThreadSearchBox.FontFamily = fontFamily;
        AgentThreadSearchBox.FontSize = fontSize;
        AgentMessageSearchBox.FontFamily = fontFamily;
        AgentMessageSearchBox.FontSize = fontSize;
    }

    private void SetupWorkspaceFilter()
    {
        _workspaceView = CollectionViewSource.GetDefaultView(ViewModel.Workspaces);
        if (_workspaceView != null)
        {
            _workspaceView.Filter = WorkspaceFilterPredicate;
            WorkspaceList.ItemsSource = _workspaceView;
        }
    }

    private bool WorkspaceFilterPredicate(object obj)
    {
        if (obj is not WorkspaceViewModel ws)
            return false;

        var query = WorkspaceFilterBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return (ws.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (ws.WorkingDirectory?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (ws.GitBranch?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (ws.AgentLabel?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void WorkspaceFilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _workspaceView?.Refresh();
    }

    private void SidebarResetSession_Click(object sender, RoutedEventArgs e)
    {
        WorkspaceFilterBox.Text = "";
        _workspaceView?.Refresh();
        var wsId = ViewModel.SelectedWorkspace?.Workspace.Id;
        if (!string.IsNullOrWhiteSpace(wsId))
            App.NotificationService.MarkWorkspaceAsRead(wsId);
    }

    private void AgentResetVisibleThread_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedAgentThreadId))
        {
            MessageBox.Show(L.T("Select a thread to clear."), L.T("Agent"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        App.AgentConversationStore.ClearMessagesForThread(_selectedAgentThreadId);
        LoadAgentMessages(_selectedAgentThreadId);
        RefreshAgentThreads();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Restore window position from session state if available
        var session = Cmux.Core.Services.SessionPersistenceService.Load();
        if (session?.Window != null)
        {
            var w = session.Window;
            if (w.Width > 0 && w.Height > 0)
            {
                Left = w.X;
                Top = w.Y;
                Width = w.Width;
                Height = w.Height;
                WindowState = w.IsMaximized ? WindowState.Maximized : WindowState.Normal;
            }
        }

        RefreshSurfaceUiState();
        UpdateSidebarLayout();
        UpdateDaemonStatus();
        UpdateWindowChrome();
        SizeChanged += (_, _) => UpdateWindowClip();
        StateChanged += (_, _) => UpdateWindowChrome();
        UpdateWindowClip();

        // Monitor daemon connection changes
        App.DaemonClient.Connected += () => Dispatcher.BeginInvoke(UpdateDaemonStatus);
        App.DaemonClient.Disconnected += () => Dispatcher.BeginInvoke(UpdateDaemonStatus);
    }

    private void UpdateDaemonStatus()
    {
        var connected = App.DaemonClient.IsConnected;
        DaemonStatusDot.Fill = connected
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0xD3, 0x99)) // green
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80)); // gray
        DaemonStatusText.Text = connected ? L.T("Daemon") : L.T("Local");
        DaemonStatusBorder.ToolTip = connected
            ? L.T("Connected to cmux-daemon — sessions persist across restarts")
            : L.T("Running locally — sessions will not persist");
    }

    private void UpdateWindowChrome()
    {
        bool maximized = WindowState == WindowState.Maximized;
        // When maximized, use zero corner radius and no border
        WindowBorder.CornerRadius = maximized ? new CornerRadius(0) : (CornerRadius)FindResource("WindowCornerRadius");
        WindowBorder.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
        // Update maximize/restore icon
        MaxRestoreIcon.Text = maximized ? "\uE923" : "\uE922";
        MaxRestoreButton.ToolTip = maximized ? L.T("Restore") : L.T("Maximize");
        UpdateWindowClip();
    }

    private void UpdateWindowClip()
    {
        double radius = WindowState == WindowState.Maximized ? 0 : 10;
        WindowClipGeometry.RadiusX = radius;
        WindowClipGeometry.RadiusY = radius;
        WindowClipGeometry.Rect = new System.Windows.Rect(0, 0, WindowBorder.ActualWidth, WindowBorder.ActualHeight);
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _uiRefreshTimer.Stop();
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        App.AgentRuntime.RuntimeUpdated -= OnAgentRuntimeUpdated;
        App.AgentConversationStore.StoreChanged -= OnAgentConversationStoreChanged;
        ViewModel.SaveSession(Left, Top, Width, Height, WindowState == WindowState.Maximized);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.AgentPanelVisible) ||
            e.PropertyName == nameof(MainViewModel.AgentPanelWidth))
        {
            UpdateAgentPanelLayout();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SidebarVisible) ||
            e.PropertyName == nameof(MainViewModel.SidebarWidth))
        {
            UpdateSidebarLayout();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedWorkspace))
        {
            RefreshAgentThreads();
            RequestTerminalFocusAfterNavigation();
        }
    }

    private void OnAgentConversationStoreChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            RefreshAgentThreads();
        });
    }

    private void OnAgentRuntimeUpdated(AgentRuntimeUpdate update)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsUpdateForCurrentPane(update))
                return;

            if (!string.IsNullOrWhiteSpace(update.ThreadId) &&
                !string.Equals(_selectedAgentThreadId, update.ThreadId, StringComparison.Ordinal))
            {
                _selectedAgentThreadId = update.ThreadId;
                SelectAgentThreadInList(update.ThreadId);
            }

            switch (update.Type)
            {
                case AgentRuntimeUpdateType.ThreadChanged:
                    RefreshAgentThreads();
                    break;

                case AgentRuntimeUpdateType.UserMessage:
                    AppendAgentMessage("user", update.Message, update.CreatedAtUtc, "-", update.ThreadId);
                    AgentStatusText.Text = L.T("User message sent");
                    break;

                case AgentRuntimeUpdateType.AssistantDelta:
                    AppendAssistantDelta(update.ThreadId, update.Message);
                    AgentStatusText.Text = L.T("Streaming response...");
                    break;

                case AgentRuntimeUpdateType.AssistantCompleted:
                    FinalizeAssistantMessage(update.ThreadId, update.Message, update.CreatedAtUtc,
                        $"usage in:{update.InputTokens} out:{update.OutputTokens} total:{update.TotalTokens} · {update.Provider}/{update.Model}");
                    AgentUsageText.Text = string.Format(L.T("Usage: in {0} · out {1} · total {2}"), update.InputTokens, update.OutputTokens, update.TotalTokens);
                    AgentContextText.Text = update.ContextBudgetTokens > 0
                        ? string.Format(
                            L.T("Context: {0}/{1} tokens{2}"),
                            update.EstimatedContextTokens,
                            update.ContextBudgetTokens,
                            update.ContextNeedsCompaction ? L.T(" (near limit)") : string.Empty)
                        : L.T("Context: -");
                    AgentStatusText.Text = L.T("Response completed");
                    RefreshAgentThreads();
                    break;

                case AgentRuntimeUpdateType.ContextMetrics:
                    AgentContextText.Text = update.ContextBudgetTokens > 0
                        ? string.Format(
                            L.T("Context: {0}/{1} tokens{2}{3}"),
                            update.EstimatedContextTokens,
                            update.ContextBudgetTokens,
                            update.ContextNeedsCompaction ? L.T(" (near limit)") : string.Empty,
                            update.CompactionApplied ? L.T(" · compacted") : string.Empty)
                        : L.T("Context: -");
                    break;

                case AgentRuntimeUpdateType.Error:
                    AppendAgentMessage("error", update.Message, update.CreatedAtUtc, "error", update.ThreadId);
                    AgentStatusText.Text = $"{L.T("Error")}: {update.Message}";
                    break;

                case AgentRuntimeUpdateType.Status:
                    AgentStatusText.Text = string.IsNullOrWhiteSpace(update.Message) ? L.T("Idle") : update.Message;
                    break;
            }
        });
    }

    private bool IsUpdateForCurrentPane(AgentRuntimeUpdate update)
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface == null)
            return false;

        return string.Equals(update.WorkspaceId, ViewModel.SelectedWorkspace?.Workspace.Id ?? "", StringComparison.Ordinal)
            && string.Equals(update.SurfaceId, surface.Surface.Id, StringComparison.Ordinal);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        // === App-level shortcuts that always work, even with terminal focus ===

        // Ctrl+Tab / Ctrl+Shift+Tab: cycle surfaces
        if (ctrl && e.Key == Key.Tab)
        {
            if (shift)
                ViewModel.SelectedWorkspace?.PreviousSurface();
            else
                ViewModel.SelectedWorkspace?.NextSurface();
            RequestTerminalFocusAfterNavigation();
            e.Handled = true;
            return;
        }

        // Ctrl+Alt: pane focus + history picker
        if (ctrl && alt && !shift)
        {
            switch (e.Key)
            {
                case Key.Right:
                case Key.Down:
                    ViewModel.SelectedWorkspace?.SelectedSurface?.FocusNextPane();
                    RequestTerminalFocusAfterNavigation();
                    e.Handled = true;
                    return;
                case Key.Left:
                case Key.Up:
                    ViewModel.SelectedWorkspace?.SelectedSurface?.FocusPreviousPane();
                    RequestTerminalFocusAfterNavigation();
                    e.Handled = true;
                    return;
                case Key.H: // Open command history picker (Ctrl+Alt+H)
                    OpenCommandHistoryPicker();
                    e.Handled = true;
                    return;
            }
        }

        // Ctrl+Shift: app-level shortcuts (split, zoom, search, surfaces, etc.)
        if (ctrl && shift && !alt)
        {
            switch (e.Key)
            {
                case Key.W: // Close workspace
                    ViewModel.CloseWorkspace(ViewModel.SelectedWorkspace);
                    e.Handled = true;
                    return;
                case Key.R: // Rename workspace
                    ViewModel.SelectedWorkspace?.Rename();
                    e.Handled = true;
                    return;
                case Key.D: // Split down
                    ViewModel.SelectedWorkspace?.SelectedSurface?.SplitDown();
                    e.Handled = true;
                    return;
                case Key.U: // Jump to latest unread
                    ViewModel.JumpToLatestUnread();
                    e.Handled = true;
                    return;
                case Key.OemCloseBrackets: // Next surface (Ctrl+Shift+])
                    ViewModel.SelectedWorkspace?.NextSurface();
                    RequestTerminalFocusAfterNavigation();
                    e.Handled = true;
                    return;
                case Key.OemOpenBrackets: // Previous surface (Ctrl+Shift+[)
                    ViewModel.SelectedWorkspace?.PreviousSurface();
                    RequestTerminalFocusAfterNavigation();
                    e.Handled = true;
                    return;
                case Key.Z: // Zoom toggle (Ctrl+Shift+Z)
                    ViewModel.SelectedWorkspace?.SelectedSurface?.ToggleZoom();
                    e.Handled = true;
                    return;
                case Key.F: // Search (Ctrl+Shift+F)
                    ToggleSearch();
                    e.Handled = true;
                    return;
                case Key.P: // Command palette (Ctrl+Shift+P)
                    ToggleCommandPalette();
                    e.Handled = true;
                    return;
                case Key.L: // Logs (Ctrl+Shift+L)
                    OpenLogsWindow();
                    e.Handled = true;
                    return;
                case Key.V: // Session Vault (Ctrl+Shift+V)
                    OpenSessionVault();
                    e.Handled = true;
                    return;
                case Key.H: // History: insert last command (Ctrl+Shift+H)
                    InsertLastCommandFromHistory();
                    e.Handled = true;
                    return;
                case Key.A: // Toggle agent chat
                    ToggleAgentChat();
                    e.Handled = true;
                    return;
                case Key.B: // Open browser
                    OpenBrowserSurface();
                    e.Handled = true;
                    return;
            }
        }

        // === Ctrl-only shortcuts (skip when terminal has focus to let terminal handle them) ===
        if (ctrl && !alt && IsTerminalFocusActive())
            return;

        // Workspaces
        if (ctrl && !shift && !alt)
        {
            switch (e.Key)
            {
                case Key.N: // New workspace
                    ViewModel.CreateNewWorkspace();
                    e.Handled = true;
                    return;
                case Key.B: // Toggle sidebar
                    ViewModel.ToggleSidebar();
                    e.Handled = true;
                    return;
                case Key.I: // Notification panel
                    ViewModel.ToggleNotificationPanel();
                    e.Handled = true;
                    return;
                case Key.T: // New surface
                    ViewModel.SelectedWorkspace?.CreateNewSurface();
                    RequestTerminalFocusAfterNavigation();
                    e.Handled = true;
                    return;
                case Key.W: // Close surface
                    var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
                    if (surface != null)
                        ViewModel.SelectedWorkspace?.CloseSurface(surface);
                    e.Handled = true;
                    return;
                case Key.D: // Split right
                    ViewModel.SelectedWorkspace?.SelectedSurface?.SplitRight();
                    RequestTerminalFocusAfterNavigation();
                    e.Handled = true;
                    return;
                // Workspace 1-8
                case Key.D1: ViewModel.SelectWorkspace(0); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D2: ViewModel.SelectWorkspace(1); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D3: ViewModel.SelectWorkspace(2); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D4: ViewModel.SelectWorkspace(3); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D5: ViewModel.SelectWorkspace(4); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D6: ViewModel.SelectWorkspace(5); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D7: ViewModel.SelectWorkspace(6); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D8: ViewModel.SelectWorkspace(7); RequestTerminalFocusAfterNavigation(); e.Handled = true; return;
                case Key.D9: // Last workspace
                    if (ViewModel.Workspaces.Count > 0)
                        ViewModel.SelectWorkspace(ViewModel.Workspaces.Count - 1);
                    RequestTerminalFocusAfterNavigation();
                    e.Handled = true;
                    return;
                case Key.OemComma: // Settings (Ctrl+,)
                    OpenSettings();
                    e.Handled = true;
                    return;
            }
        }
    }

    // Title bar handlers
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (WindowState != WindowState.Normal)
            return;

        if (sender is not Thumb thumb || thumb.Tag is not string edge)
            return;

        const double minW = 600;
        const double minH = 400;

        double left = Left;
        double top = Top;
        double width = Width;
        double height = Height;

        void ResizeLeft(double dx)
        {
            var newWidth = Math.Max(minW, width - dx);
            var delta = width - newWidth;
            width = newWidth;
            left += delta;
        }

        void ResizeRight(double dx)
        {
            width = Math.Max(minW, width + dx);
        }

        void ResizeTop(double dy)
        {
            var newHeight = Math.Max(minH, height - dy);
            var delta = height - newHeight;
            height = newHeight;
            top += delta;
        }

        void ResizeBottom(double dy)
        {
            height = Math.Max(minH, height + dy);
        }

        switch (edge)
        {
            case "Left": ResizeLeft(e.HorizontalChange); break;
            case "Right": ResizeRight(e.HorizontalChange); break;
            case "Top": ResizeTop(e.VerticalChange); break;
            case "Bottom": ResizeBottom(e.VerticalChange); break;
            case "TopLeft":
                ResizeLeft(e.HorizontalChange);
                ResizeTop(e.VerticalChange);
                break;
            case "TopRight":
                ResizeRight(e.HorizontalChange);
                ResizeTop(e.VerticalChange);
                break;
            case "BottomLeft":
                ResizeLeft(e.HorizontalChange);
                ResizeBottom(e.VerticalChange);
                break;
            case "BottomRight":
                ResizeRight(e.HorizontalChange);
                ResizeBottom(e.VerticalChange);
                break;
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    // --- Workspace drag-and-drop reordering ---

    private Point _dragStartPoint;

    private void WorkspaceItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void WorkspaceItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var diff = _dragStartPoint - e.GetPosition(null);
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is System.Windows.Controls.ListBoxItem item &&
            item.DataContext is ViewModels.WorkspaceViewModel workspace)
        {
            DragDrop.DoDragDrop(item, workspace, DragDropEffects.Move);
        }
    }

    private void WorkspaceItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is not System.Windows.Controls.ListBoxItem targetItem) return;
        if (targetItem.DataContext is not ViewModels.WorkspaceViewModel targetWorkspace) return;

        var sourceWorkspace = e.Data.GetData(typeof(ViewModels.WorkspaceViewModel)) as ViewModels.WorkspaceViewModel;
        if (sourceWorkspace == null || sourceWorkspace == targetWorkspace) return;

        int sourceIndex = ViewModel.Workspaces.IndexOf(sourceWorkspace);
        int targetIndex = ViewModel.Workspaces.IndexOf(targetWorkspace);

        if (sourceIndex >= 0 && targetIndex >= 0)
        {
            ViewModel.Workspaces.Move(sourceIndex, targetIndex);
        }
    }

    // Title bar + menu handlers
    private void CommandPalette_Click(object sender, RoutedEventArgs e) => ToggleCommandPalette();
    private void Search_Click(object sender, RoutedEventArgs e) => ToggleSearch();
    private void Snippets_Click(object sender, RoutedEventArgs e) => ToggleSnippetPicker();
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void MenuOpenLogs_Click(object sender, RoutedEventArgs e) => OpenLogsWindow();
    private void MenuOpenSessionVault_Click(object sender, RoutedEventArgs e) => OpenSessionVault();
    private void MenuOpenBrowser_Click(object sender, RoutedEventArgs e) => OpenBrowserSurface();
    private void MenuToggleAgentChat_Click(object sender, RoutedEventArgs e) => ToggleAgentChat();
    private void MenuOpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();
    private void MenuOpenKeyboardShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsWindow("Keyboard") { Owner = this };
        settings.ShowDialog();
    }
    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            L.T("cmuxw for Windows\nA terminal multiplexer for AI coding workflows with built-in browser surfaces and automation support."),
            L.T("About cmux"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // Toolbar handlers
    private void ToolbarSplitRight_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedWorkspace?.SelectedSurface?.SplitRight();
        RequestTerminalFocusAfterNavigation();
    }

    private void ToolbarSplitDown_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedWorkspace?.SelectedSurface?.SplitDown();
        RequestTerminalFocusAfterNavigation();
    }
    private void ShellSelector_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var shells = ShellDetector.DetectShells();
        var menu = new ContextMenu();

        foreach (var shell in shells)
        {
            var item = new MenuItem { Header = shell.Name, Tag = shell.Path };
            item.Click += (s, _) =>
            {
                if (s is MenuItem mi && mi.Tag is string path)
                    ViewModel.SelectedWorkspace?.SelectedSurface?.OpenPaneWithShell(path);
            };
            menu.Items.Add(item);
        }

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }
    private void ToggleAgentChat_Click(object sender, RoutedEventArgs e) => ToggleAgentChat();
    private void ToolbarLayout2Col_Click(object sender, RoutedEventArgs e) => ApplyLayout(2, 1);
    private void ToolbarLayoutGrid_Click(object sender, RoutedEventArgs e) => ApplyLayout(2, 2);
    private void ToolbarLayoutMainStack_Click(object sender, RoutedEventArgs e) => ApplyMainStackLayout();
    private void ToolbarEqualize_Click(object sender, RoutedEventArgs e) => ViewModel.SelectedWorkspace?.SelectedSurface?.EqualizePanes();
    private void ToolbarZoom_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedWorkspace?.SelectedSurface?.ToggleZoom();
        RefreshSurfaceUiState();
    }

    private void OpenBrowserSurface()
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface == null)
            return;

        var targetUrl = ResolveDefaultBrowserUrl();
        // Keep browser launch one-click, matching cmux-style behavior.
        if (surface.OpenBrowserOnRight(targetUrl))
            RequestTerminalFocusAfterNavigation();
    }

    private static string ResolveDefaultBrowserUrl()
    {
        var configured = (Cmux.Core.Config.SettingsService.Current.BrowserDefaultUrl ?? "").Trim();
        if (string.IsNullOrWhiteSpace(configured))
            return FallbackBrowserUrl;

        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return configured;
        }

        return FallbackBrowserUrl;
    }


    private void ToggleSearch()
    {
        SurfaceTabBarControl.FocusSearch();
    }

    private void ToggleSnippetPicker()
    {
        if (SnippetPickerControl.Visibility == Visibility.Visible)
        {
            BrowserPaneRegistry.PopWebViewAirspaceSuppress();
            SnippetPickerControl.Visibility = Visibility.Collapsed;
        }
        else
        {
            BrowserPaneRegistry.PushWebViewAirspaceSuppress();
            SnippetPickerControl.RefreshList();
            SnippetPickerControl.Visibility = Visibility.Visible;
            SnippetPickerControl.FocusSearch();
        }
    }

    private void OnSnippetSelected(Cmux.Core.Models.Snippet snippet)
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface?.FocusedPaneId is string paneId)
        {
            var session = surface.GetSession(paneId);
            var content = snippet.Resolve();
            session?.Write(content);
            App.SnippetService.IncrementUseCount(snippet.Id);
        }

        if (SnippetPickerControl.Visibility == Visibility.Visible)
            BrowserPaneRegistry.PopWebViewAirspaceSuppress();
        SnippetPickerControl.Visibility = Visibility.Collapsed;
    }

    private void ToggleCommandPalette()
    {
        if (CommandPaletteControl.Visibility == Visibility.Visible)
        {
            CommandPaletteControl.Hide();
        }
        else
        {
            var items = BuildPaletteItems();
            CommandPaletteControl.Show(items);
        }
    }

    private void ToggleAgentChat()
    {
        ViewModel.ToggleAgentPanel();
        UpdateAgentPanelLayout();
        if (ViewModel.AgentPanelVisible)
        {
            RefreshAgentThreads();
            AgentPromptBox.Focus();
        }
        else
        {
            FocusTerminal();
        }
    }

    private void UpdateSidebarLayout()
    {
        if (ViewModel.SidebarVisible)
        {
            var width = Math.Clamp(ViewModel.SidebarWidth, 200, 500);
            SidebarColumn.Width = new GridLength(width);
            SidebarColumn.MinWidth = 200;
            SidebarColumn.MaxWidth = 500;
            SidebarBorder.Visibility = Visibility.Visible;
            SidebarSplitter.Visibility = Visibility.Visible;
        }
        else
        {
            SidebarColumn.Width = new GridLength(0);
            SidebarColumn.MinWidth = 0;
            SidebarColumn.MaxWidth = 0;
            SidebarBorder.Visibility = Visibility.Collapsed;
            SidebarSplitter.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateAgentPanelLayout()
    {
        if (ViewModel.AgentPanelVisible)
        {
            var width = Math.Clamp(ViewModel.AgentPanelWidth, 300, 620);
            AgentChatColumn.Width = new GridLength(width);
            AgentChatPanel.Visibility = Visibility.Visible;
            ToolbarAgentChatButton.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        else
        {
            AgentChatColumn.Width = new GridLength(0);
            AgentChatPanel.Visibility = Visibility.Collapsed;
            ToolbarAgentChatButton.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundDimBrush");
        }
    }

    private (SurfaceViewModel Surface, string PaneId)? GetCurrentPaneContext()
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface == null)
            return null;

        var paneId = surface.FocusedPaneId;
        if (string.IsNullOrWhiteSpace(paneId))
        {
            paneId = surface.RootNode.GetLeaves()
                .Select(l => l.PaneId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
        }

        if (string.IsNullOrWhiteSpace(paneId))
            return null;

        return (surface, paneId);
    }

    private void RefreshAgentThreadsIfContextChanged()
    {
        var contextKey = ViewModel.SelectedWorkspace?.Workspace.Id ?? "";

        if (string.Equals(contextKey, _lastAgentContextKey, StringComparison.Ordinal))
            return;

        _lastAgentContextKey = contextKey;
        RefreshAgentThreads();
    }

    private void RefreshAgentThreads()
    {
        var context = GetCurrentPaneContext();
        var workspaceId = ViewModel.SelectedWorkspace?.Workspace.Id ?? "";
        var query = AgentThreadSearchBox.Text?.Trim() ?? "";
        IReadOnlyList<Cmux.Core.Models.AgentConversationThread> threads;

        // Keep history stable across pane/surface changes: list is workspace-wide.
        threads = string.IsNullOrWhiteSpace(query)
            ? App.AgentConversationStore.GetThreads(workspaceId, surfaceId: "", paneId: "")
            : App.AgentConversationStore.SearchThreads(workspaceId, surfaceId: "", paneId: "", query: query);

        if (threads.Count == 0 && !string.IsNullOrWhiteSpace(workspaceId))
        {
            // Last-resort fallback: show global agent history (across workspace ids).
            threads = string.IsNullOrWhiteSpace(query)
                ? App.AgentConversationStore.GetThreads(workspaceId: "", surfaceId: "", paneId: "")
                : App.AgentConversationStore.SearchThreads(workspaceId: "", surfaceId: "", paneId: "", query: query);
        }

        _agentThreadViews = threads
            .Select(t => new AgentThreadView
            {
                Id = t.Id,
                Title = t.Title,
                Meta = $"{t.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {t.MessageCount} msg · tok {t.TotalTokens}",
            })
            .ToList();

        AgentThreadsList.ItemsSource = _agentThreadViews;

        var preferredThreadId = _selectedAgentThreadId;
        if (string.IsNullOrWhiteSpace(preferredThreadId) && context != null)
            preferredThreadId = App.AgentRuntime.GetActiveThreadId(workspaceId, context.Value.Surface.Surface.Id, context.Value.PaneId);
        if (string.IsNullOrWhiteSpace(preferredThreadId))
            preferredThreadId = _agentThreadViews.FirstOrDefault()?.Id;

        if (!string.IsNullOrWhiteSpace(preferredThreadId))
        {
            var match = _agentThreadViews.FirstOrDefault(t => string.Equals(t.Id, preferredThreadId, StringComparison.Ordinal));
            if (match != null)
            {
                bool switchedThread = !string.Equals(_selectedAgentThreadId, match.Id, StringComparison.Ordinal);
                _selectedAgentThreadId = match.Id;
                var currentSelectedId = (AgentThreadsList.SelectedItem as AgentThreadView)?.Id;
                if (!string.Equals(currentSelectedId, match.Id, StringComparison.Ordinal))
                    AgentThreadsList.SelectedItem = match;

                bool needsInitialLoad = _allThreadMessages.Count == 0
                    || !_allThreadMessages.All(m => string.Equals(m.ThreadId, match.Id, StringComparison.Ordinal));

                if (switchedThread || needsInitialLoad)
                    LoadAgentMessages(match.Id);
            }
            else
            {
                // Keep currently loaded messages if user search/filter hides thread list rows.
                if (_allThreadMessages.Count == 0)
                    LoadAgentMessages("");
            }
        }
        else
        {
            if (_allThreadMessages.Count == 0)
                LoadAgentMessages("");
        }
    }

    private void SelectAgentThreadInList(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            return;

        var match = _agentThreadViews.FirstOrDefault(t => string.Equals(t.Id, threadId, StringComparison.Ordinal));
        if (match == null)
            return;

        var switchedThread = !string.Equals(_selectedAgentThreadId, match.Id, StringComparison.Ordinal);
        _selectedAgentThreadId = match.Id;

        var currentSelectedId = (AgentThreadsList.SelectedItem as AgentThreadView)?.Id;
        if (!string.Equals(currentSelectedId, match.Id, StringComparison.Ordinal))
            AgentThreadsList.SelectedItem = match;

        if (switchedThread || _allThreadMessages.Count == 0 || !_allThreadMessages.All(m => string.Equals(m.ThreadId, match.Id, StringComparison.Ordinal)))
            LoadAgentMessages(match.Id);
    }

    private void LoadAgentMessages(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            _allThreadMessages = [];
            _visibleThreadMessages.Clear();
            return;
        }

        _streamingAssistantByThread.Remove(threadId);

        var messages = App.AgentConversationStore.GetMessages(threadId, 2000)
            .Select(m =>
            {
                var role = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role.Trim().ToLowerInvariant();
                var roleLabel = role switch
                {
                    "assistant" => "assistant",
                    "system" => "system",
                    _ => role == "error" ? "error" : "user",
                };

                var meta = m.TotalTokens > 0
                    ? $"{m.CreatedAtUtc.ToLocalTime():HH:mm:ss} · tok {m.TotalTokens}"
                    : $"{m.CreatedAtUtc.ToLocalTime():HH:mm:ss}";

                if (m.InputTokens > 0 || m.OutputTokens > 0)
                    meta = $"{meta} · in {m.InputTokens} / out {m.OutputTokens}";

                if (!string.IsNullOrWhiteSpace(m.Provider) || !string.IsNullOrWhiteSpace(m.Model))
                    meta = $"{meta} · {m.Provider}/{m.Model}".TrimEnd('/');

                return new AgentChatMessageView
                {
                    ThreadId = threadId,
                    Role = roleLabel,
                    Header = $"{roleLabel} · {m.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                    Content = m.Content,
                    Meta = meta,
                    CreatedAtUtc = m.CreatedAtUtc,
                };
            })
            .ToList();

        _allThreadMessages = messages;
        ApplyAgentMessageFilter();
        ScrollAgentMessagesToBottom();
    }

    private void ApplyAgentMessageFilter()
    {
        var query = AgentMessageSearchBox.Text?.Trim() ?? "";
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allThreadMessages
            : _allThreadMessages.Where(m => MatchesMessageFilter(m, query)).ToList();

        _visibleThreadMessages.Clear();
        foreach (var msg in filtered)
            _visibleThreadMessages.Add(msg);
    }

    private void AppendAgentMessage(string role, string content, DateTime createdAtUtc, string meta, string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId) || !string.Equals(_selectedAgentThreadId, threadId, StringComparison.Ordinal))
            return;

        var last = _allThreadMessages.LastOrDefault();
        if (last != null &&
            string.Equals(last.ThreadId, threadId, StringComparison.Ordinal) &&
            string.Equals(last.Role, role, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(last.Content?.Trim(), (content ?? "").Trim(), StringComparison.Ordinal) &&
            Math.Abs((last.CreatedAtUtc - createdAtUtc).TotalSeconds) < 3)
        {
            return;
        }

        var view = new AgentChatMessageView
        {
            ThreadId = threadId,
            Role = role,
            Header = $"{role} · {createdAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
            Content = content ?? "",
            Meta = string.IsNullOrWhiteSpace(meta) ? createdAtUtc.ToLocalTime().ToString("HH:mm:ss") : meta,
            CreatedAtUtc = createdAtUtc,
        };

        _allThreadMessages.Add(view);
        var query = AgentMessageSearchBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(query) || MatchesMessageFilter(view, query))
            _visibleThreadMessages.Add(view);
        ScrollAgentMessagesToBottom();
    }

    private void AppendAssistantDelta(string threadId, string delta)
    {
        if (string.IsNullOrWhiteSpace(threadId) || !string.Equals(_selectedAgentThreadId, threadId, StringComparison.Ordinal))
            return;

        if (!_streamingAssistantByThread.TryGetValue(threadId, out var message))
        {
            message = new AgentChatMessageView
            {
                ThreadId = threadId,
                Role = "assistant",
                Header = $"{L.T("assistant")} · {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Content = "",
                Meta = L.T("streaming..."),
                CreatedAtUtc = DateTime.UtcNow,
            };
            _streamingAssistantByThread[threadId] = message;
            _allThreadMessages.Add(message);

            var query = AgentMessageSearchBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(query) || MatchesMessageFilter(message, query))
                _visibleThreadMessages.Add(message);
        }

        message.Content += delta ?? "";
        var activeQuery = AgentMessageSearchBox.Text?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(activeQuery))
        {
            var visible = _visibleThreadMessages.Contains(message);
            var matches = MatchesMessageFilter(message, activeQuery);
            if (matches && !visible)
                _visibleThreadMessages.Add(message);
            else if (!matches && visible)
                _visibleThreadMessages.Remove(message);
        }
        ScrollAgentMessagesToBottom();
    }

    private void FinalizeAssistantMessage(string threadId, string finalText, DateTime createdAtUtc, string meta)
    {
        if (string.IsNullOrWhiteSpace(threadId) || !string.Equals(_selectedAgentThreadId, threadId, StringComparison.Ordinal))
            return;

        if (_streamingAssistantByThread.TryGetValue(threadId, out var message))
        {
            message.Header = $"{L.T("assistant")} · {createdAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            message.Content = string.IsNullOrWhiteSpace(finalText) ? message.Content : finalText;
            message.Meta = meta;
            message.CreatedAtUtc = createdAtUtc;
            _streamingAssistantByThread.Remove(threadId);
        }
        else
        {
            var newMessage = new AgentChatMessageView
            {
                ThreadId = threadId,
                Role = "assistant",
                Header = $"{L.T("assistant")} · {createdAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}",
                Content = finalText ?? "",
                Meta = meta,
                CreatedAtUtc = createdAtUtc,
            };
            _allThreadMessages.Add(newMessage);

            var query = AgentMessageSearchBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(query) || MatchesMessageFilter(newMessage, query))
                _visibleThreadMessages.Add(newMessage);
        }

        if (!string.IsNullOrWhiteSpace(AgentMessageSearchBox.Text))
            ApplyAgentMessageFilter();
        ScrollAgentMessagesToBottom();
    }

    private void ScrollAgentMessagesToBottom()
    {
        if (AgentMessagesList.Items.Count > 0)
            AgentMessagesList.ScrollIntoView(AgentMessagesList.Items[AgentMessagesList.Items.Count - 1]);
    }

    private static bool IsTerminalFocusActive()
    {
        var current = Keyboard.FocusedElement as DependencyObject;
        while (current != null)
        {
            if (current is TerminalControl)
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private List<PaletteItem> BuildPaletteItems()
    {
        return
        [
            new() { Id = "new-workspace", Label = L.T("New Workspace"), Icon = "\uE710", Shortcut = "Ctrl+N", Category = L.T("Workspace"), Execute = () => ViewModel.CreateNewWorkspace() },
            new() { Id = "new-surface", Label = L.T("New Surface"), Icon = "\uE710", Shortcut = "Ctrl+T", Category = L.T("Surface"), Execute = () => ViewModel.SelectedWorkspace?.CreateNewSurface() },
            new() { Id = "new-browser", Label = L.T("New Browser"), Icon = "\uE774", Shortcut = "Ctrl+Shift+B", Category = L.T("Surface"), Execute = OpenBrowserSurface },
            new() { Id = "close-surface", Label = L.T("Close Surface"), Icon = "\uE711", Shortcut = "Ctrl+W", Category = L.T("Surface"), Execute = () => { var s = ViewModel.SelectedWorkspace?.SelectedSurface; if (s != null) ViewModel.SelectedWorkspace?.CloseSurface(s); } },
            new() { Id = "close-workspace", Label = L.T("Close Workspace"), Icon = "\uE711", Shortcut = "Ctrl+Shift+W", Category = L.T("Workspace"), Execute = () => ViewModel.CloseWorkspace(ViewModel.SelectedWorkspace) },
            new() { Id = "split-right", Label = L.T("Split Right"), Icon = "\uE26B", Shortcut = "Ctrl+D", Category = L.T("Pane"), Execute = () => ViewModel.SelectedWorkspace?.SelectedSurface?.SplitRight() },
            new() { Id = "split-down", Label = L.T("Split Down"), Icon = "\uE74B", Shortcut = "Ctrl+Shift+D", Category = L.T("Pane"), Execute = () => ViewModel.SelectedWorkspace?.SelectedSurface?.SplitDown() },
            new() { Id = "toggle-sidebar", Label = L.T("Toggle Sidebar"), Icon = "\uE700", Shortcut = "Ctrl+B", Category = L.T("View"), Execute = () => ViewModel.ToggleSidebar() },
            new() { Id = "notifications", Label = L.T("Notifications"), Icon = "\uEA8F", Shortcut = "Ctrl+I", Category = L.T("View"), Execute = () => ViewModel.ToggleNotificationPanel() },
            new() { Id = "test-notification", Label = L.T("Test Notification"), Icon = "\uE7F4", Category = L.T("View"), Execute = ShowTestNotification },
            new() { Id = "open-logs", Label = L.T("Open Command Logs"), Icon = "\uE7BA", Shortcut = "Ctrl+Shift+L", Category = L.T("Logs"), Execute = OpenLogsWindow },
            new() { Id = "open-session-vault", Label = L.T("Open Session Vault"), Icon = "\uE8D1", Shortcut = "Ctrl+Shift+V", Category = L.T("Logs"), Execute = OpenSessionVault },
            new() { Id = "open-command-history", Label = L.T("Open Command History"), Icon = "\uE81C", Shortcut = "Ctrl+Alt+H", Category = L.T("History"), Execute = OpenCommandHistoryPicker },
            new() { Id = "insert-last-command", Label = L.T("Insert Last Command"), Icon = "\uE8A7", Shortcut = "Ctrl+Shift+H", Category = L.T("History"), Execute = InsertLastCommandFromHistory },
            new() { Id = "search", Label = L.T("Search"), Icon = "\uE721", Shortcut = "Ctrl+Shift+F", Category = L.T("View"), Execute = () => ToggleSearch() },
            new() { Id = "toggle-agent-chat", Label = L.T("Toggle Agent Chat"), Icon = "\uE11B", Shortcut = "Ctrl+Shift+A", Category = L.T("View"), Execute = ToggleAgentChat },
            new() { Id = "zoom-pane", Label = L.T("Zoom Pane"), Icon = "\uE740", Shortcut = "Ctrl+Shift+Z", Category = L.T("Pane"), Execute = () => ViewModel.SelectedWorkspace?.SelectedSurface?.ToggleZoom() },
            new() { Id = "focus-next", Label = L.T("Focus Next Pane"), Icon = "\uE76C", Shortcut = "Ctrl+Alt+Right", Category = L.T("Pane"), Execute = () => ViewModel.SelectedWorkspace?.SelectedSurface?.FocusNextPane() },
            new() { Id = "focus-prev", Label = L.T("Focus Previous Pane"), Icon = "\uE76B", Shortcut = "Ctrl+Alt+Left", Category = L.T("Pane"), Execute = () => ViewModel.SelectedWorkspace?.SelectedSurface?.FocusPreviousPane() },
            new() { Id = "next-surface", Label = L.T("Next Surface"), Icon = "\uE76C", Shortcut = "Ctrl+Tab", Category = L.T("Surface"), Execute = () => ViewModel.SelectedWorkspace?.NextSurface() },
            new() { Id = "prev-surface", Label = L.T("Previous Surface"), Icon = "\uE76B", Shortcut = "Ctrl+Shift+Tab", Category = L.T("Surface"), Execute = () => ViewModel.SelectedWorkspace?.PreviousSurface() },
            new() { Id = "settings", Label = L.T("Settings"), Icon = "\uE713", Shortcut = "Ctrl+,", Category = L.T("App"), Execute = () => OpenSettings() },
            new() { Id = "equalize", Label = L.T("Equalize Panes"), Icon = "\uE9D5", Category = L.T("Pane"), Execute = () => ViewModel.SelectedWorkspace?.SelectedSurface?.EqualizePanes() },
            new() { Id = "layout-2col", Label = L.T("Layout: 2 Columns"), Icon = "\uE745", Category = L.T("Layout"), Execute = () => ApplyLayout(2, 1) },
            new() { Id = "layout-3col", Label = L.T("Layout: 3 Columns"), Icon = "\uE745", Category = L.T("Layout"), Execute = () => ApplyLayout(3, 1) },
            new() { Id = "layout-grid", Label = L.T("Layout: Grid 2x2"), Icon = "\uF0E2", Category = L.T("Layout"), Execute = () => ApplyLayout(2, 2) },
            new() { Id = "layout-main-stack", Label = L.T("Layout: Main + Stack"), Icon = "\uE745", Category = L.T("Layout"), Execute = () => ApplyMainStackLayout() },
        ];
    }

    private void ApplyLayout(int cols, int rows)
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface == null) return;

        var target = cols * rows;
        var n = surface.RootNode.GetLeaves().Count();
        var layout = Cmux.Core.Models.SplitNode.ComputePaneGridLayout(surface.RootNode);
        var dense = Cmux.Core.Models.SplitNode.IsDenseRectangularGrid(layout, n);

        if (n > target)
        {
            List<string> toClose;
            if (dense)
            {
                toClose = surface.GetOrderedPaneIdsToCloseForGridResize(cols, rows).ToList();
            }
            else
            {
                var keep = ComputeKeepPaneIdsForShrink(surface, target);
                var keepSet = keep.ToHashSet(StringComparer.Ordinal);
                toClose = surface.RootNode.GetLeaves()
                    .Select(l => l.PaneId!)
                    .Where(id => !keepSet.Contains(id))
                    .Reverse()
                    .ToList();
            }

            if (toClose.Count > 0)
            {
                var line = string.Format(L.T("Layout shrink will close N panes"), toClose.Count);
                if (toClose.Any(surface.PaneHasNotableActivity))
                    line += "\n\n" + L.T("Layout shrink active sessions hint");

                if (MessageBox.Show(
                        line,
                        L.T("Change layout"),
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning) != MessageBoxResult.OK)
                    return;

                foreach (var id in toClose)
                    surface.ClosePane(id);
            }

            n = surface.RootNode.GetLeaves().Count();
            layout = Cmux.Core.Models.SplitNode.ComputePaneGridLayout(surface.RootNode);
            dense = Cmux.Core.Models.SplitNode.IsDenseRectangularGrid(layout, n);
        }

        if (n == target)
        {
            if (!Cmux.Core.Models.SplitNode.MatchesRequestedGrid(layout, cols, rows, n))
            {
                var ids = surface.RootNode.GetLeaves()
                    .Select(l => l.PaneId!)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
                if (ids.Count == target)
                    surface.ReplaceRootNode(Cmux.Core.Models.SplitNode.BuildDenseGridRowMajor(ids, cols, rows));
            }

            surface.EqualizePanes();
            return;
        }

        if (n < target)
        {
            if (dense)
            {
                surface.ExpandDenseGridTo(cols, rows);
                return;
            }

            for (var c = 1; c < cols; c++)
                surface.SplitRight();

            if (rows > 1)
            {
                var columnPaneIds = surface.RootNode.GetLeaves()
                    .Select(l => l.PaneId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Cast<string>()
                    .ToList();

                foreach (var paneId in columnPaneIds)
                {
                    surface.FocusPane(paneId);
                    for (var r = 1; r < rows; r++)
                        surface.SplitDown();
                }
            }

            layout = Cmux.Core.Models.SplitNode.ComputePaneGridLayout(surface.RootNode);
            n = surface.RootNode.GetLeaves().Count();
            if (n == target && !Cmux.Core.Models.SplitNode.MatchesRequestedGrid(layout, cols, rows, n))
            {
                var ids = surface.RootNode.GetLeaves()
                    .Select(l => l.PaneId!)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
                if (ids.Count == target)
                    surface.ReplaceRootNode(Cmux.Core.Models.SplitNode.BuildDenseGridRowMajor(ids, cols, rows));
            }

            surface.EqualizePanes();
            return;
        }

        surface.EqualizePanes();
    }

    /// <summary>Pane ids to keep when shrinking a non-dense layout: focused first, then activity, then leaf order.</summary>
    private static List<string> ComputeKeepPaneIdsForShrink(SurfaceViewModel surface, int target)
    {
        var all = surface.RootNode.GetLeaves()
            .Select(l => l.PaneId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < all.Count; i++)
            index[all[i]] = i;

        var focus = surface.FocusedPaneId;
        var keep = new List<string>(target);

        if (!string.IsNullOrWhiteSpace(focus) && all.Contains(focus))
            keep.Add(focus!);

        foreach (var id in all
                     .Where(id => !keep.Contains(id, StringComparer.Ordinal))
                     .OrderByDescending(id => surface.PaneHasNotableActivity(id) ? 1 : 0)
                     .ThenBy(id => index[id]))
        {
            if (keep.Count >= target)
                break;
            keep.Add(id);
        }

        return keep;
    }

    private void ApplyMainStackLayout()
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface == null) return;

        var n = surface.RootNode.GetLeaves().Count();
        if (n == 3 && IsMainStackLayout(surface.RootNode))
        {
            surface.EqualizePanes();
            return;
        }

        if (n == 1)
        {
            surface.SplitRight();

            var rightPaneId = surface.RootNode.GetLeaves()
                .Skip(1)
                .Select(l => l.PaneId)
                .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

            if (!string.IsNullOrWhiteSpace(rightPaneId))
            {
                surface.FocusPane(rightPaneId);
                surface.SplitDown();
                surface.EqualizePanes();
            }

            return;
        }

        if (n > 1)
        {
            var line = string.Format(L.T("Main stack layout will close N panes"), Math.Max(0, n - 1));
            if (surface.RootNode.GetLeaves().Any(l => l.PaneId != null && surface.PaneHasNotableActivity(l.PaneId!)))
                line += "\n\n" + L.T("Layout shrink active sessions hint");

            if (MessageBox.Show(
                    line,
                    L.T("Change layout"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;
        }

        NormalizeToSinglePane(surface);

        surface.SplitRight();

        var stackPaneId = surface.RootNode.GetLeaves()
            .Skip(1)
            .Select(l => l.PaneId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        if (!string.IsNullOrWhiteSpace(stackPaneId))
        {
            surface.FocusPane(stackPaneId);
            surface.SplitDown();
            surface.EqualizePanes();
        }
    }

    /// <summary>Detects the standard Main+Stack (1 main + 2 horizontal stack) three-pane tree.</summary>
    private static bool IsMainStackLayout(Cmux.Core.Models.SplitNode root)
    {
        if (root is not { IsLeaf: false, Direction: Cmux.Core.Models.SplitDirection.Vertical, First: not null, Second: not null })
            return false;
        if (!root.First.IsLeaf || root.Second.IsLeaf)
            return false;

        var stack = root.Second;
        if (stack is not { IsLeaf: false, Direction: Cmux.Core.Models.SplitDirection.Horizontal, First: not null, Second: not null })
            return false;

        return stack.First.IsLeaf
            && stack.Second.IsLeaf
            && root.GetLeaves().Count() == 3;
    }

    private static void NormalizeToSinglePane(SurfaceViewModel surface)
    {
        var paneIds = surface.RootNode.GetLeaves()
            .Select(l => l.PaneId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();

        if (paneIds.Count <= 1) return;

        var focusedPaneId = surface.FocusedPaneId;
        string keepPaneId = !string.IsNullOrWhiteSpace(focusedPaneId) && paneIds.Contains(focusedPaneId)
            ? focusedPaneId
            : paneIds[0];

        surface.FocusPane(keepPaneId);

        foreach (var paneId in paneIds.Where(id => id != keepPaneId))
            surface.ClosePane(paneId);
    }

    private void FocusTerminal()
    {
        if (!SplitPaneContainerControl.FocusActivePane())
            ContentArea.Focus();
    }

    private void RequestTerminalFocusAfterNavigation()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!SplitPaneContainerControl.FocusActivePane())
                ContentArea.Focus();
        }, DispatcherPriority.Input);
    }

    private void RefreshSurfaceUiState()
    {
        RefreshAgentThreadsIfContextChanged();

        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface == null)
        {
            PaneCountText.Text = L.T("0 panes");
            ToolbarZoomIcon.Text = "\uE740";
            ToolbarZoomButton.ToolTip = $"{L.T("Zoom Pane")} (Ctrl+Shift+Z)";
            return;
        }

        var paneCount = surface.RootNode.GetLeaves().Count();
        PaneCountText.Text = surface.IsZoomed
            ? string.Format(L.T("{0} panes (1 zoomed)"), paneCount)
            : paneCount == 1 ? L.T("1 pane") : string.Format(L.T("{0} panes"), paneCount);

        ToolbarZoomIcon.Text = surface.IsZoomed ? "\uE73F" : "\uE740";
        ToolbarZoomButton.ToolTip = surface.IsZoomed
            ? $"{L.T("Unzoom Pane")} (Ctrl+Shift+Z)"
            : $"{L.T("Zoom Pane")} (Ctrl+Shift+Z)";
    }

    // --- Search handling ---
    private int _currentSearchMatch = 0;
    private List<(int row, int col, int length)> _searchMatches = [];

    private void OnSearchTextChanged(string query)
    {
        _searchMatches = [];
        _currentSearchMatch = 0;

        if (string.IsNullOrEmpty(query))
        {
            ClearSearchHighlights();
            SurfaceTabBarControl.UpdateMatchCount(0, 0);
            return;
        }

        // Search in focused terminal
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface?.FocusedPaneId is string paneId)
        {
            var session = surface.GetSession(paneId);
            if (session != null)
            {
                _searchMatches = FindAllInBuffer(session.Buffer, query);
                _currentSearchMatch = 0;
                UpdateSearchHighlights();
            }
        }

        SurfaceTabBarControl.UpdateMatchCount(_currentSearchMatch, _searchMatches.Count);
    }

    private void OnSearchNext()
    {
        if (_searchMatches.Count == 0) return;
        _currentSearchMatch = (_currentSearchMatch + 1) % _searchMatches.Count;
        UpdateSearchHighlights();
        SurfaceTabBarControl.UpdateMatchCount(_currentSearchMatch, _searchMatches.Count);
    }

    private void OnSearchPrevious()
    {
        if (_searchMatches.Count == 0) return;
        _currentSearchMatch = (_currentSearchMatch - 1 + _searchMatches.Count) % _searchMatches.Count;
        UpdateSearchHighlights();
        SurfaceTabBarControl.UpdateMatchCount(_currentSearchMatch, _searchMatches.Count);
    }

    private void OnSearchClosed()
    {
        ClearSearchHighlights();
        _searchMatches = [];
    }

    private void UpdateSearchHighlights()
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface?.FocusedPaneId is string paneId)
        {
            var terminal = FindTerminalForPane(paneId);
            terminal?.SetSearchHighlights(_searchMatches, _currentSearchMatch);
        }
    }

    private void ClearSearchHighlights()
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface?.FocusedPaneId is string paneId)
        {
            var terminal = FindTerminalForPane(paneId);
            terminal?.ClearSearchHighlights();
        }
    }

    private TerminalControl? FindTerminalForPane(string paneId)
    {
        return FindVisualChild<TerminalControl>(ContentArea, null);
    }

    private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate) where T : DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && (predicate == null || predicate(typed)))
                return typed;
            var result = FindVisualChild(child, predicate);
            if (result != null) return result;
        }
        return null;
    }

    private static List<(int row, int col, int length)> FindAllInBuffer(Cmux.Core.Terminal.TerminalBuffer buffer, string query)
    {
        var matches = new List<(int, int, int)>();
        if (string.IsNullOrEmpty(query)) return matches;

        for (int row = 0; row < buffer.Rows; row++)
        {
            var lineText = GetRowText(buffer, row);
            int idx = 0;
            while ((idx = lineText.IndexOf(query, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                matches.Add((row, idx, query.Length));
                idx++;
            }
        }
        return matches;
    }

    private static string GetRowText(Cmux.Core.Terminal.TerminalBuffer buffer, int row)
    {
        var sb = new System.Text.StringBuilder();
        for (int col = 0; col < buffer.Cols; col++)
        {
            var cell = buffer.CellAt(row, col);
            sb.Append(cell.Character == '\0' ? ' ' : cell.Character);
        }
        return sb.ToString();
    }

    private void ShowTestNotification()
    {
        var workspaceId = ViewModel.SelectedWorkspace?.Workspace.Id ?? string.Empty;
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        var surfaceId = surface?.Surface.Id ?? string.Empty;
        var paneId = surface?.FocusedPaneId;

        App.NotificationService.AddNotification(
            workspaceId,
            surfaceId,
            paneId,
            L.T("cmux test"),
            L.T("Notification check"),
            L.T("If you see this in panel/toast, notifications are working."),
            Cmux.Core.Models.NotificationSource.Cli);
    }

    private void OpenLogsWindow()
    {
        var window = new LogsWindow { Owner = this };
        window.ShowDialog();
    }

    private void OpenSessionVault()
    {
        var window = new SessionVaultWindow { Owner = this };
        window.ShowDialog();
    }

    private void OpenCommandHistoryPicker()
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface?.FocusedPaneId is not string paneId)
            return;

        var history = surface.GetCommandHistory(paneId);
        if (history.Count == 0)
        {
            MessageBox.Show(L.T("No command history found yet for this pane."), L.T("History"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var paneLabel = paneId.Length <= 8 ? paneId : paneId[..8];
        var window = new HistoryWindow(
            history,
            insertAction: command => surface.GetSession(paneId)?.Write(command),
            runAction: command =>
            {
                surface.RegisterCommandSubmission(paneId, command);
                surface.GetSession(paneId)?.Write(command + Environment.NewLine);
            })
        {
            Owner = this,
            Title = $"{L.T("Command History")} · {L.T("Pane")} {paneLabel}",
        };

        window.ShowDialog();
    }

    private void InsertLastCommandFromHistory()
    {
        var surface = ViewModel.SelectedWorkspace?.SelectedSurface;
        if (surface?.FocusedPaneId is not string paneId)
            return;

        var history = surface.GetCommandHistory(paneId);
        if (history.Count == 0)
        {
            MessageBox.Show(L.T("No command history found yet for this pane."), L.T("History"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var last = history[^1];
        surface.GetSession(paneId)?.Write(last);
    }

    private void AgentThreadSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshAgentThreads();
    }

    private void AgentRefreshThreads_Click(object sender, RoutedEventArgs e)
    {
        RefreshAgentThreads();
    }

    private void AgentNewThread_Click(object sender, RoutedEventArgs e)
    {
        var context = GetCurrentPaneContext();
        if (context == null)
            return;

        var workspaceId = ViewModel.SelectedWorkspace?.Workspace.Id ?? "";
        var thread = App.AgentConversationStore.CreateThread(
            workspaceId,
            context.Value.Surface.Surface.Id,
            context.Value.PaneId,
            Cmux.Core.Config.SettingsService.Current.Agent.AgentName);

        App.AgentRuntime.SetActiveThreadId(workspaceId, context.Value.Surface.Surface.Id, context.Value.PaneId, thread.Id);
        _selectedAgentThreadId = thread.Id;
        RefreshAgentThreads();
        SelectAgentThreadInList(thread.Id);
        AgentStatusText.Text = L.T("New thread created");
    }

    private void AgentThreadsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AgentThreadsList.SelectedItem is not AgentThreadView selected)
            return;

        var switchedThread = !string.Equals(_selectedAgentThreadId, selected.Id, StringComparison.Ordinal);
        _selectedAgentThreadId = selected.Id;

        var context = GetCurrentPaneContext();
        if (context != null)
        {
            var workspaceId = ViewModel.SelectedWorkspace?.Workspace.Id ?? "";
            App.AgentRuntime.SetActiveThreadId(workspaceId, context.Value.Surface.Surface.Id, context.Value.PaneId, selected.Id);
        }

        if (switchedThread || _allThreadMessages.Count == 0 || !_allThreadMessages.All(m => string.Equals(m.ThreadId, selected.Id, StringComparison.Ordinal)))
            LoadAgentMessages(selected.Id);
    }

    private void AgentMessageSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyAgentMessageFilter();
    }

    private static bool MatchesMessageFilter(AgentChatMessageView message, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return (message.Content?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (message.Header?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (message.Meta?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void AgentPromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            SendAgentPromptFromPanel();
            e.Handled = true;
        }
    }

    private void AgentSend_Click(object sender, RoutedEventArgs e)
    {
        SendAgentPromptFromPanel();
    }

    private void SendAgentPromptFromPanel()
    {
        var prompt = AgentPromptBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        var context = GetCurrentPaneContext();
        if (context == null)
        {
            AgentStatusText.Text = L.T("No active pane selected");
            return;
        }

        var workspaceId = ViewModel.SelectedWorkspace?.Workspace.Id ?? "";
        var threadId = _selectedAgentThreadId;
        if (string.IsNullOrWhiteSpace(threadId))
        {
            var created = App.AgentConversationStore.CreateThread(
                workspaceId,
                context.Value.Surface.Surface.Id,
                context.Value.PaneId,
                Cmux.Core.Config.SettingsService.Current.Agent.AgentName);
            threadId = created.Id;
            _selectedAgentThreadId = threadId;
        }

        App.AgentRuntime.SetActiveThreadId(workspaceId, context.Value.Surface.Surface.Id, context.Value.PaneId, threadId);

        var session = context.Value.Surface.GetSession(context.Value.PaneId);
        bool accepted = App.AgentRuntime.TrySendChatPrompt(
            prompt,
            new AgentPaneContext
            {
                WorkspaceId = workspaceId,
                SurfaceId = context.Value.Surface.Surface.Id,
                PaneId = context.Value.PaneId,
                WorkingDirectory = session?.WorkingDirectory,
                WriteToPane = text =>
                {
                    if (!string.IsNullOrWhiteSpace(text))
                        session?.Write(text);
                },
            },
            threadId);

        if (!accepted)
        {
            AgentStatusText.Text = L.T("Agent did not accept the prompt");
            return;
        }

        AgentPromptBox.Text = "";
        AgentStatusText.Text = L.T("Prompt sent");
        RefreshAgentThreads();
        if (!string.IsNullOrWhiteSpace(threadId))
            SelectAgentThreadInList(threadId);
    }

    private void OpenSettings()
    {
        var settings = new SettingsWindow { Owner = this };
        settings.ShowDialog();
    }

    private sealed class AgentThreadView
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string Meta { get; init; } = "";
    }

    private sealed class AgentChatMessageView : INotifyPropertyChanged
    {
        private string _threadId = "";
        private string _role = "";
        private string _header = "";
        private string _content = "";
        private string _meta = "";
        private DateTime _createdAtUtc = DateTime.UtcNow;

        public string ThreadId
        {
            get => _threadId;
            set
            {
                if (string.Equals(_threadId, value, StringComparison.Ordinal))
                    return;
                _threadId = value;
                OnPropertyChanged(nameof(ThreadId));
            }
        }

        public string Role
        {
            get => _role;
            set
            {
                if (string.Equals(_role, value, StringComparison.Ordinal))
                    return;
                _role = value;
                OnPropertyChanged(nameof(Role));
            }
        }

        public string Header
        {
            get => _header;
            set
            {
                if (string.Equals(_header, value, StringComparison.Ordinal))
                    return;
                _header = value;
                OnPropertyChanged(nameof(Header));
            }
        }

        public string Content
        {
            get => _content;
            set
            {
                if (string.Equals(_content, value, StringComparison.Ordinal))
                    return;
                _content = value;
                OnPropertyChanged(nameof(Content));
            }
        }

        public string Meta
        {
            get => _meta;
            set
            {
                if (string.Equals(_meta, value, StringComparison.Ordinal))
                    return;
                _meta = value;
                OnPropertyChanged(nameof(Meta));
            }
        }

        public DateTime CreatedAtUtc
        {
            get => _createdAtUtc;
            set
            {
                if (_createdAtUtc == value)
                    return;
                _createdAtUtc = value;
                OnPropertyChanged(nameof(CreatedAtUtc));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
