using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cmux.Core.IPC;
using Cmux.Core.Models;
using Cmux.Core.Services;

namespace Cmux.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<WorkspaceViewModel> _workspaces = [];

    [ObservableProperty]
    private WorkspaceViewModel? _selectedWorkspace;

    [ObservableProperty]
    private bool _sidebarVisible = true;

    [ObservableProperty]
    private double _sidebarWidth = 280;

    [ObservableProperty]
    private bool _compactSidebar;

    private double _sidebarWidthBeforeCompact = 280;

    public bool IsSidebarExpanded => !CompactSidebar;

    [ObservableProperty]
    private bool _notificationPanelVisible;

    [ObservableProperty]
    private int _totalUnreadCount;

    [ObservableProperty]
    private bool _agentPanelVisible = true;

    [ObservableProperty]
    private double _agentPanelWidth = 380;

    private readonly NotificationService _notificationService;
    private readonly Dictionary<string, string> _namedBuffers = new(StringComparer.Ordinal);
    private string _defaultBuffer = "";

    public NotificationService NotificationService => _notificationService;

    public MainViewModel()
    {
        _notificationService = App.NotificationService;
        _notificationService.UnreadCountChanged += () =>
        {
            TotalUnreadCount = _notificationService.UnreadCount;
            UpdateWorkspaceNotificationCounts();
        };

        // Wire up the named pipe command handler
        if (App.PipeServer != null)
        {
            App.PipeServer.OnCommand = HandlePipeCommand;
        }

        // Restore session or create default workspace
        var session = SessionPersistenceService.Load();
        if (session != null && session.Workspaces.Count > 0)
        {
            RestoreSession(session);
        }
        else
        {
            CreateNewWorkspace();
        }
    }

    [RelayCommand]
    public void CreateNewWorkspace()
    {
        var workspace = new Workspace { Name = $"Workspace {Workspaces.Count + 1}" };
        var surface = new Surface { Name = "Terminal 1" };
        workspace.Surfaces.Add(surface);
        workspace.SelectedSurface = surface;

        var vm = new WorkspaceViewModel(workspace, _notificationService);
        Workspaces.Add(vm);
        SelectedWorkspace = vm;
    }

    public void DuplicateWorkspace(WorkspaceViewModel source)
    {
        var clone = new Workspace
        {
            Name = source.Name + " (copy)",
            IconGlyph = source.IconGlyph,
            AccentColor = source.AccentColor,
            WorkingDirectory = source.WorkingDirectory,
            ExplorerState = new WorkspaceExplorerState
            {
                SelectedPath = source.Workspace.ExplorerState.SelectedPath,
                ExpandedPaths = source.Workspace.ExplorerState.ExpandedPaths.ToList(),
                Roots = source.Workspace.ExplorerState.Roots
                    .Select(r => new ExplorerRootConfig
                    {
                        Id = Guid.NewGuid().ToString(),
                        Path = r.Path,
                        DisplayName = r.DisplayName,
                    })
                    .ToList(),
            },
        };

        var surfaceMap = new Dictionary<string, Surface>();

        foreach (var sourceSurfaceVm in source.Surfaces)
        {
            var sourceSurface = sourceSurfaceVm.Surface;
            var paneIdMap = new Dictionary<string, string>();
            var clonedRoot = CloneSplitNode(sourceSurface.RootSplitNode, paneIdMap);

            var clonedSurface = new Surface
            {
                Name = sourceSurface.Name,
                RootSplitNode = clonedRoot,
                FocusedPaneId = sourceSurface.FocusedPaneId != null && paneIdMap.TryGetValue(sourceSurface.FocusedPaneId, out var mappedFocused)
                    ? mappedFocused
                    : clonedRoot.GetLeaves().Select(l => l.PaneId).FirstOrDefault(),
            };

            foreach (var (oldPaneId, customName) in sourceSurface.PaneCustomNames)
            {
                if (paneIdMap.TryGetValue(oldPaneId, out var newPaneId))
                    clonedSurface.PaneCustomNames[newPaneId] = customName;
            }

            foreach (var (oldPaneId, browserUrl) in sourceSurface.BrowserPaneUrls)
            {
                if (paneIdMap.TryGetValue(oldPaneId, out var newPaneId))
                    clonedSurface.BrowserPaneUrls[newPaneId] = browserUrl;
            }

            foreach (var (oldPaneId, snapshot) in sourceSurface.PaneSnapshots)
            {
                if (!paneIdMap.TryGetValue(oldPaneId, out var newPaneId))
                    continue;

                clonedSurface.PaneSnapshots[newPaneId] = new PaneStateSnapshot
                {
                    CapturedAt = snapshot.CapturedAt,
                    WorkingDirectory = snapshot.WorkingDirectory,
                    Shell = snapshot.Shell,
                    CommandHistory = snapshot.CommandHistory.ToList(),
                    BufferSnapshot = snapshot.BufferSnapshot == null
                        ? null
                        : new Cmux.Core.Terminal.TerminalBufferSnapshot
                        {
                            Cols = snapshot.BufferSnapshot.Cols,
                            Rows = snapshot.BufferSnapshot.Rows,
                            CursorRow = snapshot.BufferSnapshot.CursorRow,
                            CursorCol = snapshot.BufferSnapshot.CursorCol,
                            ScrollbackLines = snapshot.BufferSnapshot.ScrollbackLines.ToList(),
                            ScreenLines = snapshot.BufferSnapshot.ScreenLines.ToList(),
                        },
                };
            }

            clone.Surfaces.Add(clonedSurface);
            surfaceMap[sourceSurface.Id] = clonedSurface;
        }

        clone.SelectedSurface = source.SelectedSurface != null && surfaceMap.TryGetValue(source.SelectedSurface.Surface.Id, out var selected)
            ? selected
            : clone.Surfaces.FirstOrDefault();

        if (clone.Surfaces.Count == 0)
        {
            var fallbackSurface = new Surface { Name = "Terminal 1" };
            clone.Surfaces.Add(fallbackSurface);
            clone.SelectedSurface = fallbackSurface;
        }

        var vm = new WorkspaceViewModel(clone, _notificationService);
        Workspaces.Add(vm);
        SelectedWorkspace = vm;
    }

    [RelayCommand]
    public void CloseWorkspace(WorkspaceViewModel? workspace)
    {
        if (workspace == null) return;
        if (Workspaces.Count <= 1) return; // Keep at least one

        int index = Workspaces.IndexOf(workspace);
        workspace.CaptureAllSurfaceTranscripts("workspace-close");
        workspace.Dispose();
        Workspaces.Remove(workspace);

        if (SelectedWorkspace == workspace)
        {
            SelectedWorkspace = Workspaces[Math.Min(index, Workspaces.Count - 1)];
        }
    }

    [RelayCommand]
    public void SelectWorkspace(int index)
    {
        if (index >= 0 && index < Workspaces.Count)
        {
            SelectedWorkspace = Workspaces[index];
        }
    }

    [RelayCommand]
    public void NextWorkspace()
    {
        if (Workspaces.Count == 0) return;
        int index = SelectedWorkspace != null ? Workspaces.IndexOf(SelectedWorkspace) : -1;
        SelectedWorkspace = Workspaces[(index + 1) % Workspaces.Count];
    }

    [RelayCommand]
    public void PreviousWorkspace()
    {
        if (Workspaces.Count == 0) return;
        int index = SelectedWorkspace != null ? Workspaces.IndexOf(SelectedWorkspace) : 0;
        SelectedWorkspace = Workspaces[(index - 1 + Workspaces.Count) % Workspaces.Count];
    }

    [RelayCommand]
    public void ToggleSidebar() => SidebarVisible = !SidebarVisible;

    [RelayCommand]
    public void ToggleCompactSidebar() => CompactSidebar = !CompactSidebar;

    [RelayCommand]
    public void ToggleNotificationPanel() => NotificationPanelVisible = !NotificationPanelVisible;

    [RelayCommand]
    public void ToggleAgentPanel() => AgentPanelVisible = !AgentPanelVisible;

    partial void OnCompactSidebarChanged(bool value)
    {
        if (value)
        {
            if (SidebarWidth > 120)
                _sidebarWidthBeforeCompact = SidebarWidth;

            SidebarWidth = 92;
        }
        else
        {
            SidebarWidth = Math.Max(220, _sidebarWidthBeforeCompact);
        }

        OnPropertyChanged(nameof(IsSidebarExpanded));
    }

    [RelayCommand]
    public void JumpToLatestUnread()
    {
        var latest = _notificationService.GetLatestUnread();
        if (latest != null)
            NavigateToNotification(latest);
    }

    public void NavigateToNotification(TerminalNotification notification)
    {
        var workspace = Workspaces.FirstOrDefault(w => w.Workspace.Id == notification.WorkspaceId);
        if (workspace != null)
        {
            SelectedWorkspace = workspace;
            var surface = workspace.Surfaces.FirstOrDefault(s => s.Surface.Id == notification.SurfaceId);
            if (surface != null)
            {
                workspace.SelectedSurface = surface;
                if (notification.PaneId != null)
                {
                    surface.FocusPane(notification.PaneId);
                }
            }
            _notificationService.MarkAsRead(notification.Id);
        }

        // Close the notification panel after navigating
        if (NotificationPanelVisible)
            ToggleNotificationPanel();
    }

    [RelayCommand]
    public void MarkAllNotificationsRead()
    {
        _notificationService.MarkAllAsRead();
    }

    private void UpdateWorkspaceNotificationCounts()
    {
        foreach (var ws in Workspaces)
        {
            ws.UnreadNotificationCount = _notificationService.GetUnreadCount(ws.Workspace.Id);
            ws.LatestNotificationText = _notificationService.GetLatestText(ws.Workspace.Id);
        }
    }

    public void SaveSession(double windowX, double windowY, double windowWidth, double windowHeight, bool isMaximized)
    {
        // Capture terminal transcripts and in-memory terminal context before serializing.
        foreach (var workspace in Workspaces)
        {
            workspace.Explorer.CaptureStateForPersistence();
            foreach (var surface in workspace.Surfaces)
            {
                surface.SyncWorkingDirectoryHintsForPersistence();
                surface.CaptureAllPaneTranscripts("session-close");
                surface.CapturePaneSnapshotsForPersistence();
            }
        }

        var workspaces = Workspaces.Select(w => w.Workspace).ToList();
        var state = SessionPersistenceService.BuildState(
            workspaces,
            SelectedWorkspace != null ? Workspaces.IndexOf(SelectedWorkspace) : null,
            windowX, windowY, windowWidth, windowHeight,
            isMaximized, SidebarWidth, SidebarVisible, CompactSidebar,
            AgentPanelWidth, AgentPanelVisible, NotificationPanelVisible);
        SessionPersistenceService.Save(state);
    }

    private void RestoreSession(SessionState session)
    {
        foreach (var wsState in session.Workspaces)
        {
            var workspace = new Workspace
            {
                Id = wsState.Id,
                Name = wsState.Name,
                IconGlyph = string.IsNullOrWhiteSpace(wsState.IconGlyph) ? "\uE8A5" : wsState.IconGlyph,
                AccentColor = string.IsNullOrWhiteSpace(wsState.AccentColor) ? "#FF818CF8" : wsState.AccentColor,
                WorkingDirectory = wsState.WorkingDirectory,
                ExplorerState = wsState.ExplorerState ?? new WorkspaceExplorerState(),
            };

            foreach (var surfState in wsState.Surfaces)
            {
                var surface = new Surface
                {
                    Id = surfState.Id,
                    Name = surfState.Name,
                    FocusedPaneId = surfState.FocusedPaneId,
                    PaneCustomNames = new Dictionary<string, string>(surfState.PaneCustomNames),
                    BrowserPaneUrls = new Dictionary<string, string>(surfState.BrowserPaneUrls),
                    PaneSnapshots = surfState.PaneSnapshots.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new PaneStateSnapshot
                        {
                            CapturedAt = kvp.Value.CapturedAt,
                            WorkingDirectory = kvp.Value.WorkingDirectory,
                            Shell = kvp.Value.Shell,
                            CommandHistory = kvp.Value.CommandHistory.ToList(),
                            BufferSnapshot = kvp.Value.BufferSnapshot == null
                                ? null
                                : new Cmux.Core.Terminal.TerminalBufferSnapshot
                                {
                                    Cols = kvp.Value.BufferSnapshot.Cols,
                                    Rows = kvp.Value.BufferSnapshot.Rows,
                                    CursorRow = kvp.Value.BufferSnapshot.CursorRow,
                                    CursorCol = kvp.Value.BufferSnapshot.CursorCol,
                                    ScrollbackLines = kvp.Value.BufferSnapshot.ScrollbackLines.ToList(),
                                    ScreenLines = kvp.Value.BufferSnapshot.ScreenLines.ToList(),
                                },
                        }),
                };

                if (surfState.RootNode != null)
                {
                    surface.RootSplitNode = SessionPersistenceService.DeserializeSplitNode(surfState.RootNode);
                }

                workspace.Surfaces.Add(surface);
            }

            if (wsState.SelectedSurfaceIndex.HasValue &&
                wsState.SelectedSurfaceIndex.Value >= 0 &&
                wsState.SelectedSurfaceIndex.Value < workspace.Surfaces.Count)
            {
                workspace.SelectedSurface = workspace.Surfaces[wsState.SelectedSurfaceIndex.Value];
            }
            else if (workspace.Surfaces.Count > 0)
            {
                workspace.SelectedSurface = workspace.Surfaces[0];
            }

            var vm = new WorkspaceViewModel(workspace, _notificationService);
            Workspaces.Add(vm);
        }

        if (session.SelectedWorkspaceIndex.HasValue &&
            session.SelectedWorkspaceIndex.Value >= 0 &&
            session.SelectedWorkspaceIndex.Value < Workspaces.Count)
        {
            SelectedWorkspace = Workspaces[session.SelectedWorkspaceIndex.Value];
        }
        else if (Workspaces.Count > 0)
        {
            SelectedWorkspace = Workspaces[0];
        }

        if (session.Window != null)
        {
            SidebarWidth = Math.Clamp(session.Window.SidebarWidth, 220, 500);
            SidebarVisible = session.Window.SidebarVisible;
            CompactSidebar = session.Window.CompactSidebar;
            AgentPanelWidth = Math.Clamp(session.Window.AgentPanelWidth, 300, 620);
            AgentPanelVisible = session.Window.AgentPanelVisible;
            if (session.Window.NotificationPanelVisible is bool npv)
                NotificationPanelVisible = npv;
        }
    }

    private static SplitNode CloneSplitNode(SplitNode source, Dictionary<string, string> paneIdMap)
    {
        var clone = new SplitNode
        {
            IsLeaf = source.IsLeaf,
            Direction = source.Direction,
            SplitRatio = source.SplitRatio,
        };

        if (source.IsLeaf)
        {
            var oldPaneId = source.PaneId;
            var newPaneId = Guid.NewGuid().ToString();
            clone.PaneId = newPaneId;

            if (!string.IsNullOrWhiteSpace(oldPaneId))
                paneIdMap[oldPaneId] = newPaneId;
        }
        else
        {
            clone.First = source.First != null ? CloneSplitNode(source.First, paneIdMap) : null;
            clone.Second = source.Second != null ? CloneSplitNode(source.Second, paneIdMap) : null;
        }

        return clone;
    }

    private async Task<string> HandlePipeCommand(string command, Dictionary<string, string> args)
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            return command switch
            {
                "NOTIFY" => HandleNotifyCommand(args),
                "WORKSPACE.LIST" => HandleWorkspaceList(),
                "WORKSPACE.CREATE" => HandleWorkspaceCreate(args),
                "WORKSPACE.SELECT" => HandleWorkspaceSelect(args),
                "WORKSPACE.CLOSE" => HandleWorkspaceClose(args),
                "WORKSPACE.RENAME" => HandleWorkspaceRename(args),
                "WORKSPACE.CURRENT" => HandleWorkspaceCurrent(args),
                "WORKSPACE.NEXT" => HandleWorkspaceAction("next"),
                "WORKSPACE.PREVIOUS" => HandleWorkspaceAction("previous"),
                "SURFACE.CREATE" => HandleSurfaceCreate(args),
                "SURFACE.SELECT" => HandleSurfaceSelect(args),
                "SURFACE.LIST" => HandleSurfaceList(args),
                "SURFACE.CLOSE" => HandleSurfaceClose(args),
                "SURFACE.NEXT" => HandleSurfaceAction(args, "next"),
                "SURFACE.PREVIOUS" => HandleSurfaceAction(args, "previous"),
                "SPLIT.RIGHT" => HandleSplit(args, SplitDirection.Vertical),
                "SPLIT.DOWN" => HandleSplit(args, SplitDirection.Horizontal),
                "PANE.LIST" => HandlePaneList(args),
                "PANE.FOCUS" => HandlePaneFocus(args),
                "PANE.WRITE" => HandlePaneWrite(args),
                "PANE.READ" => HandlePaneRead(args),
                "PANE.FORWARD" => HandlePaneForward(args),
                "BROWSER.OPEN" => HandleBrowserOpen(args),
                "BROWSER.LIST" => HandleBrowserList(args),
                "BROWSER.SELECT" => HandleBrowserSelect(args),
                "BROWSER.CLOSE" => HandleBrowserClose(args),
                "BROWSER.SNAPSHOT" => HandleBrowserSnapshot(args),
                "BROWSER.SCREENSHOT" => HandleBrowserScreenshot(args),
                "BROWSER.CLICK" => HandleBrowserClick(args),
                "BROWSER.FILL" => HandleBrowserFill(args),
                "BROWSER.TYPE" => HandleBrowserType(args),
                "BROWSER.EVAL" => HandleBrowserEval(args),
                "TREE.LIST" => HandleTreeList(args),
                "IDENTIFY" => HandleIdentify(args),
                "CAPTURE.PANE" => HandleCapturePane(args),
                "BUFFER.SET" => HandleSetBuffer(args),
                "BUFFER.PASTE" => HandlePasteBuffer(args),
                "DISPLAY.MESSAGE" => HandleDisplayMessage(args),
                "CLAUDE.HOOK" => HandleClaudeHook(args),
                "LOG.EVENT" => HandleLogEvent(args),
                "SET.STATUS" => HandleSetStatus(args),
                "TRIGGER.FLASH" => HandleTriggerFlash(args),
                "STATUS" => HandleStatus(),
                _ => JsonSerializer.Serialize(new { error = $"Unknown command: {command}" }),
            };
        });
    }

    private string HandleNotifyCommand(Dictionary<string, string> args)
    {
        var title = args.GetValueOrDefault("title", "Terminal");
        var body = args.GetValueOrDefault("body", "");
        var subtitle = args.GetValueOrDefault("subtitle");
        var workspaceId = SelectedWorkspace?.Workspace.Id ?? "";
        var surfaceId = SelectedWorkspace?.SelectedSurface?.Surface.Id ?? "";

        _notificationService.AddNotification(
            workspaceId, surfaceId, null,
            title, subtitle, body,
            NotificationSource.Cli);

        return JsonSerializer.Serialize(new { ok = true });
    }

    private string HandleWorkspaceList()
    {
        var list = Workspaces.Select(w => new
        {
            id = w.Workspace.Id,
            name = w.Workspace.Name,
            selected = w == SelectedWorkspace,
            surfaces = w.Surfaces.Count,
        });
        return JsonSerializer.Serialize(list);
    }

    private string HandleWorkspaceCreate(Dictionary<string, string> args)
    {
        CreateNewWorkspace();
        var ws = Workspaces[^1];
        if (args.TryGetValue("name", out var name))
            ws.Name = name;
        return JsonSerializer.Serialize(new { id = ws.Workspace.Id, name = ws.Name });
    }

    private string HandleWorkspaceSelect(Dictionary<string, string> args)
    {
        if (args.TryGetValue("index", out var indexStr) && int.TryParse(indexStr, out int index))
        {
            if (TryResolveCollectionIndex(index, Workspaces.Count, out var resolvedIndex))
            {
                SelectWorkspace(resolvedIndex);
                return JsonSerializer.Serialize(new { ok = true });
            }
        }
        if (args.TryGetValue("id", out var id))
        {
            var ws = Workspaces.FirstOrDefault(w => w.Workspace.Id == id);
            if (ws != null)
            {
                SelectedWorkspace = ws;
                return JsonSerializer.Serialize(new { ok = true });
            }
        }
        if (args.TryGetValue("name", out var name))
        {
            var ws = Workspaces.FirstOrDefault(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? Workspaces.FirstOrDefault(w => w.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            if (ws != null)
            {
                SelectedWorkspace = ws;
                return JsonSerializer.Serialize(new { ok = true });
            }
        }
        return JsonSerializer.Serialize(new { error = "Workspace not found" });
    }

    private string HandleWorkspaceClose(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (Workspaces.Count <= 1)
            return JsonSerializer.Serialize(new { error = "Cannot close the last workspace." });

        var workspaceId = workspace.Workspace.Id;
        CloseWorkspace(workspace);

        return JsonSerializer.Serialize(new { ok = true, workspaceId });
    }

    private string HandleWorkspaceRename(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        var title = args.GetValueOrDefault("title")
            ?? args.GetValueOrDefault("name")
            ?? args.GetValueOrDefault("_arg0")
            ?? "";

        if (string.IsNullOrWhiteSpace(title))
            return JsonSerializer.Serialize(new { error = "Missing required argument: title" });

        workspace.Name = title.Trim();
        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
        });
    }

    private string HandleWorkspaceCurrent(Dictionary<string, string> args)
    {
        var workspace = SelectedWorkspace ?? Workspaces.FirstOrDefault();
        if (workspace == null)
            return JsonSerializer.Serialize(new { error = "No workspace available." });

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
        });
    }

    private string HandleWorkspaceAction(string action)
    {
        switch (action.ToLowerInvariant())
        {
            case "next":
                NextWorkspace();
                break;
            case "previous":
                PreviousWorkspace();
                break;
            default:
                return JsonSerializer.Serialize(new { error = $"Unsupported workspace action: {action}" });
        }

        var workspace = SelectedWorkspace;
        return JsonSerializer.Serialize(new
        {
            ok = true,
            action,
            workspaceId = workspace?.Workspace.Id ?? "",
            workspaceName = workspace?.Name ?? "",
        });
    }

    private string HandleSurfaceCreate(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;
        workspace.CreateNewSurface();

        var surface = workspace.SelectedSurface;
        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface?.Surface.Id ?? "",
            surfaceName = surface?.Name ?? "",
        });
    }

    private string HandleSurfaceSelect(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;
        workspace.SelectedSurface = surface;

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
        });
    }

    private string HandleSurfaceList(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        var surfaces = workspace.Surfaces
            .Select((s, idx) => new
            {
                index = idx + 1,
                id = s.Surface.Id,
                name = s.Name,
                selected = s == workspace.SelectedSurface,
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            workspace = new
            {
                id = workspace.Workspace.Id,
                name = workspace.Name,
            },
            surfaces,
        });
    }

    private string HandleSurfaceClose(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (workspace.Surfaces.Count <= 1)
            return JsonSerializer.Serialize(new { error = "Cannot close the last surface." });

        var surfaceId = surface.Surface.Id;
        workspace.CloseSurface(surface);

        return JsonSerializer.Serialize(new { ok = true, workspaceId = workspace.Workspace.Id, surfaceId });
    }

    private string HandleSurfaceAction(Dictionary<string, string> args, string action)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;

        switch (action.ToLowerInvariant())
        {
            case "next":
                workspace.NextSurface();
                break;
            case "previous":
                workspace.PreviousSurface();
                break;
            default:
                return JsonSerializer.Serialize(new { error = $"Unsupported surface action: {action}" });
        }

        var surface = workspace.SelectedSurface;
        return JsonSerializer.Serialize(new
        {
            ok = true,
            action,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface?.Surface.Id ?? "",
            surfaceName = surface?.Name ?? "",
        });
    }

    private string HandleSplit(Dictionary<string, string> args, SplitDirection direction)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;
        workspace.SelectedSurface = surface;
        surface.SplitFocused(direction);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            direction = direction == SplitDirection.Vertical ? "right" : "down",
        });
    }

    private string HandlePaneList(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        var leaves = surface.RootNode.GetLeaves()
            .Select(l => l.PaneId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();

        var panes = leaves
            .Select((paneId, idx) =>
            {
                surface.Surface.PaneCustomNames.TryGetValue(paneId, out var customName);
                return new
                {
                    index = idx + 1,
                    id = paneId,
                    name = string.IsNullOrWhiteSpace(customName) ? $"Pane {idx + 1}" : customName,
                    customName = customName ?? "",
                    focused = string.Equals(surface.FocusedPaneId, paneId, StringComparison.Ordinal),
                    workingDirectory = surface.GetSession(paneId)?.WorkingDirectory ?? "",
                };
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            workspace = new
            {
                id = workspace.Workspace.Id,
                name = workspace.Name,
            },
            surface = new
            {
                id = surface.Surface.Id,
                name = surface.Name,
            },
            panes,
        });
    }

    private string HandlePaneFocus(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;
        workspace.SelectedSurface = surface;
        surface.FocusPane(paneId);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
        });
    }

    private string HandlePaneWrite(Dictionary<string, string> args)
    {
        var text = args.TryGetValue("text", out var requestedText) ? (requestedText ?? "") : "";

        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        var session = surface.GetSession(paneId);
        if (session == null)
            return JsonSerializer.Serialize(new { error = $"Pane session not found: {paneId}" });

        bool submit = args.TryGetValue("submit", out var submitRaw)
            && bool.TryParse(submitRaw, out var parsedSubmit)
            && parsedSubmit;

        if (submit)
            text = text.TrimEnd('\r', '\n');

        if (!submit && string.IsNullOrWhiteSpace(text))
            return JsonSerializer.Serialize(new { error = "Missing required argument: text" });

        var submitKey = args.TryGetValue("submitKey", out var submitKeyRaw)
            ? submitKeyRaw ?? ""
            : "auto";

        if (!string.IsNullOrEmpty(text))
            session.Write(text);

        if (submit)
        {
            var submitSequence = ResolveSubmitSequence(submitKey);
            if (!string.IsNullOrEmpty(submitSequence))
                session.Write(submitSequence);

            if (!string.IsNullOrWhiteSpace(text))
                surface.RegisterCommandSubmission(paneId, text);
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
            submit,
            submitKey,
            bytes = text.Length,
        });
    }

    private string HandlePaneRead(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        var session = surface.GetSession(paneId);
        if (session == null)
            return JsonSerializer.Serialize(new { error = $"Pane session not found: {paneId}" });

        int lines = 80;
        if (args.TryGetValue("lines", out var linesRaw) && int.TryParse(linesRaw, out var parsedLines))
            lines = Math.Clamp(parsedLines, 1, 5000);

        int maxChars = 20000;
        if (args.TryGetValue("maxChars", out var charsRaw) && int.TryParse(charsRaw, out var parsedChars))
            maxChars = Math.Clamp(parsedChars, 512, 200000);

        var includeScrollback = ParseBoolArg(args.GetValueOrDefault("scrollback"), defaultValue: false);
        var allText = session.Buffer.ExportPlainText(maxScrollbackLines: includeScrollback ? 20000 : 0);
        var tailText = TailLines(allText, lines);
        if (tailText.Length > maxChars)
            tailText = "..." + tailText[^maxChars..];

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
            lines,
            maxChars,
            text = tailText,
        });
    }

    private string HandlePaneForward(Dictionary<string, string> args)
    {
        var sourceArgs = ExtractPrefixedArgs(args, "from");
        var targetArgs = ExtractPrefixedArgs(args, "to");

        if (!targetArgs.ContainsKey("paneId")
            && !targetArgs.ContainsKey("paneName")
            && !targetArgs.ContainsKey("paneIndex"))
        {
            return JsonSerializer.Serialize(new { error = "Target pane is required. Use --toPaneId, --toPaneName, or --toPaneIndex." });
        }

        if (!TryResolveWorkspace(sourceArgs, out var sourceWorkspace, out var sourceError))
            return JsonSerializer.Serialize(new { error = sourceError });

        if (!TryResolveSurface(sourceWorkspace, sourceArgs, out var sourceSurface, out sourceError))
            return JsonSerializer.Serialize(new { error = sourceError });

        if (!TryResolvePaneId(sourceSurface, sourceArgs, out var sourcePaneId, out var sourcePaneIndex, out var sourcePaneName, out sourceError))
            return JsonSerializer.Serialize(new { error = sourceError });

        if (!TryResolveWorkspace(targetArgs, out var targetWorkspace, out var targetError))
            return JsonSerializer.Serialize(new { error = targetError });

        if (!TryResolveSurface(targetWorkspace, targetArgs, out var targetSurface, out targetError))
            return JsonSerializer.Serialize(new { error = targetError });

        if (!TryResolvePaneId(targetSurface, targetArgs, out var targetPaneId, out var targetPaneIndex, out var targetPaneName, out targetError))
            return JsonSerializer.Serialize(new { error = targetError });

        var sourceSession = sourceSurface.GetSession(sourcePaneId);
        if (sourceSession == null)
            return JsonSerializer.Serialize(new { error = $"Source pane session not found: {sourcePaneId}" });

        var targetSession = targetSurface.GetSession(targetPaneId);
        if (targetSession == null)
            return JsonSerializer.Serialize(new { error = $"Target pane session not found: {targetPaneId}" });

        string payload;
        if (args.TryGetValue("text", out var explicitText) && !string.IsNullOrWhiteSpace(explicitText))
        {
            payload = explicitText;
        }
        else
        {
            int lines = 80;
            if (args.TryGetValue("lines", out var linesRaw) && int.TryParse(linesRaw, out var parsedLines))
                lines = Math.Clamp(parsedLines, 1, 5000);

            int maxChars = 20000;
            if (args.TryGetValue("maxChars", out var charsRaw) && int.TryParse(charsRaw, out var parsedChars))
                maxChars = Math.Clamp(parsedChars, 512, 200000);

            var allText = sourceSession.Buffer.ExportPlainText(maxScrollbackLines: 20000);
            payload = TailLines(allText, lines);
            if (payload.Length > maxChars)
                payload = payload[^maxChars..];
        }

        bool submit = ParseBoolArg(args.GetValueOrDefault("submit"), defaultValue: false);
        var submitKey = args.GetValueOrDefault("submitKey", "auto");

        if (!string.IsNullOrEmpty(payload))
            targetSession.Write(payload);

        if (submit)
        {
            var submitSequence = ResolveSubmitSequence(submitKey);
            if (!string.IsNullOrEmpty(submitSequence))
                targetSession.Write(submitSequence);

            if (!string.IsNullOrWhiteSpace(payload))
                targetSurface.RegisterCommandSubmission(targetPaneId, payload);
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            source = new
            {
                workspaceId = sourceWorkspace.Workspace.Id,
                workspaceName = sourceWorkspace.Name,
                surfaceId = sourceSurface.Surface.Id,
                surfaceName = sourceSurface.Name,
                paneId = sourcePaneId,
                paneIndex = sourcePaneIndex,
                paneName = sourcePaneName,
            },
            target = new
            {
                workspaceId = targetWorkspace.Workspace.Id,
                workspaceName = targetWorkspace.Name,
                surfaceId = targetSurface.Surface.Id,
                surfaceName = targetSurface.Name,
                paneId = targetPaneId,
                paneIndex = targetPaneIndex,
                paneName = targetPaneName,
            },
            bytes = payload.Length,
            submit,
            submitKey,
        });
    }

    private string HandleBrowserOpen(Dictionary<string, string> args)
    {
        var url = (args.GetValueOrDefault("url") ?? args.GetValueOrDefault("_arg0") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url))
            return JsonSerializer.Serialize(new { error = "Missing required argument: url" });

        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        var surfaceName = args.GetValueOrDefault("name");
        var browserSurface = workspace.CreateBrowserSurface(url, surfaceName);
        SelectedWorkspace = workspace;
        workspace.SelectedSurface = browserSurface;

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = browserSurface.Surface.Id,
            surfaceId = browserSurface.Surface.Id,
            surfaceName = browserSurface.Name,
            url,
        });
    }

    private string HandleBrowserList(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        var browsers = workspace.Surfaces
            .Where(s => s.IsBrowserSurface)
            .Select((surface, idx) => new
            {
                index = idx + 1,
                browserId = surface.Surface.Id,
                surfaceId = surface.Surface.Id,
                name = surface.Name,
                url = surface.GetPrimaryBrowserUrl() ?? "",
                selected = workspace.SelectedSurface == surface,
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            workspace = new
            {
                id = workspace.Workspace.Id,
                name = workspace.Name,
            },
            browsers,
        });
    }

    private string HandleBrowserSelect(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveBrowserSurface(workspace, args, out var browserSurface, out error))
            return JsonSerializer.Serialize(new { error });

        SelectedWorkspace = workspace;
        workspace.SelectedSurface = browserSurface;

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = browserSurface.Surface.Id,
            surfaceId = browserSurface.Surface.Id,
            surfaceName = browserSurface.Name,
            url = browserSurface.GetPrimaryBrowserUrl() ?? "",
        });
    }

    private string HandleBrowserClose(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveBrowserSurface(workspace, args, out var browserSurface, out error))
            return JsonSerializer.Serialize(new { error });

        if (workspace.Surfaces.Count <= 1)
            return JsonSerializer.Serialize(new { error = "Cannot close the last surface." });

        var browserId = browserSurface.Surface.Id;
        workspace.CloseSurface(browserSurface);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId,
        });
    }

    private string HandleBrowserSnapshot(Dictionary<string, string> args)
    {
        if (!TryResolveBrowserControl(args, out var workspace, out var surface, out var paneId, out var browser, out var error))
            return JsonSerializer.Serialize(new { error });

        var snapshot = Cmux.Services.PlaywrightEngineAdapter.Instance.SnapshotAsync(browser).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = surface.Surface.Id,
            paneId,
            url = browser.GetCurrentUrl(),
            snapshot,
        });
    }

    private string HandleBrowserClick(Dictionary<string, string> args)
    {
        if (!TryResolveBrowserControl(args, out var workspace, out var surface, out var paneId, out var browser, out var error))
            return JsonSerializer.Serialize(new { error });

        var selector = (args.GetValueOrDefault("selector") ?? args.GetValueOrDefault("_arg0") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(selector))
            return JsonSerializer.Serialize(new { error = "Missing required argument: selector" });

        Cmux.Services.PlaywrightEngineAdapter.Instance.ClickAsync(browser, selector).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = surface.Surface.Id,
            paneId,
            selector,
            url = browser.GetCurrentUrl(),
        });
    }

    private string HandleBrowserFill(Dictionary<string, string> args)
    {
        if (!TryResolveBrowserControl(args, out var workspace, out var surface, out var paneId, out var browser, out var error))
            return JsonSerializer.Serialize(new { error });

        var selector = (args.GetValueOrDefault("selector") ?? args.GetValueOrDefault("_arg0") ?? "").Trim();
        var value = args.GetValueOrDefault("value") ?? args.GetValueOrDefault("_arg1") ?? "";
        if (string.IsNullOrWhiteSpace(selector))
            return JsonSerializer.Serialize(new { error = "Missing required argument: selector" });

        Cmux.Services.PlaywrightEngineAdapter.Instance.FillAsync(browser, selector, value).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = surface.Surface.Id,
            paneId,
            selector,
            valueLength = value.Length,
            url = browser.GetCurrentUrl(),
        });
    }

    private string HandleBrowserType(Dictionary<string, string> args)
    {
        if (!TryResolveBrowserControl(args, out var workspace, out var surface, out var paneId, out var browser, out var error))
            return JsonSerializer.Serialize(new { error });

        var selector = (args.GetValueOrDefault("selector") ?? args.GetValueOrDefault("_arg0") ?? "").Trim();
        var value = args.GetValueOrDefault("value") ?? args.GetValueOrDefault("_arg1") ?? "";
        if (string.IsNullOrWhiteSpace(selector))
            return JsonSerializer.Serialize(new { error = "Missing required argument: selector" });

        Cmux.Services.PlaywrightEngineAdapter.Instance.TypeAsync(browser, selector, value).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = surface.Surface.Id,
            paneId,
            selector,
            valueLength = value.Length,
            url = browser.GetCurrentUrl(),
        });
    }

    private string HandleBrowserEval(Dictionary<string, string> args)
    {
        if (!TryResolveBrowserControl(args, out var workspace, out var surface, out var paneId, out var browser, out var error))
            return JsonSerializer.Serialize(new { error });

        var script = args.GetValueOrDefault("script") ?? args.GetValueOrDefault("_arg0") ?? "";
        if (string.IsNullOrWhiteSpace(script))
            return JsonSerializer.Serialize(new { error = "Missing required argument: script" });

        var result = Cmux.Services.PlaywrightEngineAdapter.Instance.EvalAsync(browser, script).GetAwaiter().GetResult();
        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = surface.Surface.Id,
            paneId,
            url = browser.GetCurrentUrl(),
            result,
        });
    }

    private string HandleBrowserScreenshot(Dictionary<string, string> args)
    {
        if (!TryResolveBrowserControl(args, out var workspace, out var surface, out var paneId, out var browser, out var error))
            return JsonSerializer.Serialize(new { error });

        var outPath = (args.GetValueOrDefault("out") ?? args.GetValueOrDefault("path") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(outPath))
            return JsonSerializer.Serialize(new { error = "Missing required argument: out" });

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(outPath));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        browser.CaptureScreenshotAsync(fullPath).GetAwaiter().GetResult();

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            browserId = surface.Surface.Id,
            paneId,
            outPath = fullPath,
            url = browser.GetCurrentUrl(),
        });
    }

    private string HandleTreeList(Dictionary<string, string> args)
    {
        var includeAll = ParseBoolArg(args.GetValueOrDefault("all"), defaultValue: true);
        var asJson = ParseBoolArg(args.GetValueOrDefault("json"), defaultValue: false);
        var targets = includeAll
            ? Workspaces.ToList()
            : (SelectedWorkspace != null ? [SelectedWorkspace] : Workspaces.Take(1).ToList());

        var treeNodes = targets.Select((workspace, workspaceIdx) =>
        {
            var workspaceRef = $"workspace:{workspaceIdx + 1}";
            var surfaces = workspace.Surfaces.Select((surface, surfaceIdx) => new
            {
                refId = $"surface:{surfaceIdx + 1}",
                id = surface.Surface.Id,
                name = surface.Name,
                selected = workspace.SelectedSurface == surface,
                isBrowser = surface.IsBrowserSurface,
            }).ToList();

            return new
            {
                refId = workspaceRef,
                id = workspace.Workspace.Id,
                name = workspace.Name,
                selected = workspace == SelectedWorkspace,
                surfaces,
            };
        }).ToList();

        if (asJson)
            return JsonSerializer.Serialize(new { ok = true, workspaces = treeNodes });

        var lines = new List<string>();
        foreach (var workspace in treeNodes)
        {
            lines.Add($"{workspace.refId} \"{workspace.name}\"");
            foreach (var surface in workspace.surfaces)
            {
                var browserMark = surface.isBrowser ? " [browser]" : "";
                lines.Add($"  {surface.refId} \"{surface.name}\"{browserMark}");
            }
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            tree = string.Join(Environment.NewLine, lines),
        });
    }

    private string HandleIdentify(Dictionary<string, string> args)
    {
        var workspace = SelectedWorkspace ?? Workspaces.FirstOrDefault();
        var workspaceRef = "workspace:1";
        var surfaceRef = "surface:1";

        if (workspace != null)
        {
            var workspaceIndex = Math.Max(0, Workspaces.IndexOf(workspace)) + 1;
            workspaceRef = $"workspace:{workspaceIndex}";
            var surface = workspace.SelectedSurface ?? workspace.Surfaces.FirstOrDefault();
            if (surface != null)
            {
                var surfaceIndex = Math.Max(0, workspace.Surfaces.IndexOf(surface)) + 1;
                surfaceRef = $"surface:{surfaceIndex}";
            }
        }

        return JsonSerializer.Serialize(new
        {
            caller = new
            {
                surface_ref = surfaceRef,
                workspace_ref = workspaceRef,
            },
        });
    }

    private string HandleCapturePane(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out _, out _, out error))
            return JsonSerializer.Serialize(new { error });

        var session = surface.GetSession(paneId);
        if (session == null)
            return JsonSerializer.Serialize(new { error = $"Pane session not found: {paneId}" });

        var includeScrollback = ParseBoolArg(args.GetValueOrDefault("scrollback"), defaultValue: false);
        var lines = 80;
        if (args.TryGetValue("lines", out var linesRaw) && int.TryParse(linesRaw, out var parsedLines))
            lines = Math.Clamp(parsedLines, 1, 5000);

        var allText = session.Buffer.ExportPlainText(maxScrollbackLines: includeScrollback ? 20000 : 0);
        var text = TailLines(allText, lines);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            text,
        });
    }

    private string HandleSetBuffer(Dictionary<string, string> args)
    {
        var name = (args.GetValueOrDefault("name") ?? "").Trim();
        var text = args.GetValueOrDefault("text")
            ?? args.GetValueOrDefault("message")
            ?? "";

        if (string.IsNullOrEmpty(text))
            text = args.GetValueOrDefault("_arg0") ?? "";

        if (string.IsNullOrWhiteSpace(name))
            _defaultBuffer = text;
        else
            _namedBuffers[name] = text;

        return JsonSerializer.Serialize(new
        {
            ok = true,
            name = string.IsNullOrWhiteSpace(name) ? "default" : name,
            length = text.Length,
        });
    }

    private string HandlePasteBuffer(Dictionary<string, string> args)
    {
        var name = (args.GetValueOrDefault("name") ?? "").Trim();
        string text;
        if (!string.IsNullOrWhiteSpace(name) && _namedBuffers.TryGetValue(name, out var namedText))
            text = namedText;
        else
            text = _defaultBuffer;
        if (string.IsNullOrEmpty(text))
            return JsonSerializer.Serialize(new { error = "Buffer is empty." });

        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        var session = surface.GetSession(paneId);
        if (session == null)
            return JsonSerializer.Serialize(new { error = $"Pane session not found: {paneId}" });

        session.Write(text);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            name = string.IsNullOrWhiteSpace(name) ? "default" : name,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
            bytes = text.Length,
        });
    }

    private string HandleDisplayMessage(Dictionary<string, string> args)
    {
        var text = (args.GetValueOrDefault("text")
            ?? args.GetValueOrDefault("message")
            ?? args.GetValueOrDefault("_arg0")
            ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return JsonSerializer.Serialize(new { error = "Missing required argument: text" });

        var workspaceId = SelectedWorkspace?.Workspace.Id ?? "";
        var surfaceId = SelectedWorkspace?.SelectedSurface?.Surface.Id ?? "";
        _notificationService.AddNotification(workspaceId, surfaceId, null, "cmux", null, text, NotificationSource.Cli);

        return JsonSerializer.Serialize(new { ok = true, text });
    }

    private static string HandleClaudeHook(Dictionary<string, string> args)
    {
        return JsonSerializer.Serialize(new { ok = true });
    }

    private static string HandleLogEvent(Dictionary<string, string> args)
    {
        return JsonSerializer.Serialize(new { ok = true });
    }

    private string HandleStatus()
    {
        return JsonSerializer.Serialize(new
        {
            version = "0.1.3",
            workspaces = Workspaces.Count,
            selectedWorkspace = SelectedWorkspace?.Workspace.Id,
            unreadNotifications = TotalUnreadCount,
        });
    }

    private string HandleSetStatus(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        var key = (args.GetValueOrDefault("key") ?? "").Trim().ToLowerInvariant();
        var value = (args.GetValueOrDefault("value")
            ?? args.GetValueOrDefault("text")
            ?? args.GetValueOrDefault("_arg1")
            ?? "").Trim();

        if (string.IsNullOrWhiteSpace(key))
            key = (args.GetValueOrDefault("_arg0") ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(key))
            return JsonSerializer.Serialize(new { error = "Missing required argument: key" });

        switch (key)
        {
            case "branch":
                workspace.GitBranch = value;
                break;
            case "pr":
            case "pr_number":
            case "pr-number":
                workspace.Workspace.LinkedPrNumber = value;
                break;
            case "pr_status":
            case "pr-status":
                workspace.Workspace.LinkedPrStatus = value;
                break;
            case "status":
            case "note":
            case "latest":
                workspace.LatestNotificationText = value;
                workspace.Workspace.LatestNotificationText = value;
                break;
            default:
                return JsonSerializer.Serialize(new { error = $"Unsupported status key: {key}" });
        }

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            key,
            value,
        });
    }

    private string HandleTriggerFlash(Dictionary<string, string> args)
    {
        if (!TryResolveWorkspace(args, out var workspace, out var error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolveSurface(workspace, args, out var surface, out error))
            return JsonSerializer.Serialize(new { error });

        if (!TryResolvePaneId(surface, args, out var paneId, out var paneIndex, out var paneName, out error))
            return JsonSerializer.Serialize(new { error });

        var session = surface.GetSession(paneId);
        if (session == null)
            return JsonSerializer.Serialize(new { error = $"Pane session not found: {paneId}" });

        // BEL triggers visual bell in TerminalControl.
        session.Write("\a");

        return JsonSerializer.Serialize(new
        {
            ok = true,
            workspaceId = workspace.Workspace.Id,
            workspaceName = workspace.Name,
            surfaceId = surface.Surface.Id,
            surfaceName = surface.Name,
            paneId,
            paneIndex,
            paneName,
        });
    }

    private bool TryResolveBrowserControl(
        Dictionary<string, string> args,
        out WorkspaceViewModel workspace,
        out SurfaceViewModel surface,
        out string paneId,
        out Cmux.Controls.BrowserControl browser,
        out string error)
    {
        workspace = null!;
        surface = null!;
        paneId = "";
        browser = null!;
        error = "";

        if (!TryResolveWorkspace(args, out workspace, out error))
            return false;

        if (!TryResolveBrowserSurface(workspace, args, out surface, out error))
            return false;

        paneId = surface.GetPrimaryBrowserPaneId() ?? "";
        if (string.IsNullOrWhiteSpace(paneId))
        {
            error = "Browser pane not found in selected browser surface.";
            return false;
        }

        var resolvedBrowser = Cmux.Services.BrowserPaneRegistry.Get(paneId);
        if (resolvedBrowser == null)
        {
            error = "Browser control is not ready yet. Select the browser surface in UI and retry.";
            return false;
        }

        browser = resolvedBrowser;

        return true;
    }

    private bool TryResolveWorkspace(Dictionary<string, string> args, out WorkspaceViewModel workspace, out string error)
    {
        workspace = null!;
        error = "";

        var defaultWorkspace = SelectedWorkspace ?? Workspaces.FirstOrDefault();
        if (defaultWorkspace == null)
        {
            error = "No workspace available.";
            return false;
        }

        workspace = defaultWorkspace;

        var workspaceRef = args.GetValueOrDefault("workspace")
            ?? args.GetValueOrDefault("workspaceRef")
            ?? args.GetValueOrDefault("workspaceId");

        if (!string.IsNullOrWhiteSpace(workspaceRef))
        {
            var byId = Workspaces.FirstOrDefault(w => string.Equals(w.Workspace.Id, workspaceRef, StringComparison.Ordinal));
            if (byId != null)
            {
                workspace = byId;
                return true;
            }

            if (TryParseRefIndex(workspaceRef, "workspace", out var workspaceRefIndex)
                && TryResolveCollectionIndex(workspaceRefIndex, Workspaces.Count, out var resolvedRefIndex))
            {
                workspace = Workspaces[resolvedRefIndex];
                return true;
            }

            if (int.TryParse(workspaceRef, out var workspaceNumeric)
                && TryResolveCollectionIndex(workspaceNumeric, Workspaces.Count, out var resolvedNumericIndex))
            {
                workspace = Workspaces[resolvedNumericIndex];
                return true;
            }

            error = $"Workspace id/ref/index not found: {workspaceRef}";
            return false;
        }

        if (args.TryGetValue("workspaceName", out var workspaceName) && !string.IsNullOrWhiteSpace(workspaceName))
        {
            var byName = Workspaces.FirstOrDefault(w => string.Equals(w.Name, workspaceName, StringComparison.OrdinalIgnoreCase))
                ?? Workspaces.FirstOrDefault(w => w.Name.Contains(workspaceName, StringComparison.OrdinalIgnoreCase));
            if (byName == null)
            {
                error = $"Workspace name not found: {workspaceName}";
                return false;
            }

            workspace = byName;
            return true;
        }

        if (args.TryGetValue("workspaceIndex", out var workspaceIndexRaw)
            && int.TryParse(workspaceIndexRaw, out var workspaceIndex))
        {
            if (!TryResolveCollectionIndex(workspaceIndex, Workspaces.Count, out var resolvedIndex))
            {
                error = $"Workspace index out of range: {workspaceIndex}";
                return false;
            }

            workspace = Workspaces[resolvedIndex];
            return true;
        }

        return true;
    }

    private static bool TryResolveSurface(WorkspaceViewModel workspace, Dictionary<string, string> args, out SurfaceViewModel surface, out string error)
    {
        surface = null!;
        error = "";

        var defaultSurface = workspace.SelectedSurface ?? workspace.Surfaces.FirstOrDefault();
        if (defaultSurface == null)
        {
            error = "No surface available in workspace.";
            return false;
        }

        surface = defaultSurface;

        var surfaceRef = args.GetValueOrDefault("surface")
            ?? args.GetValueOrDefault("surfaceRef")
            ?? args.GetValueOrDefault("surfaceId");

        if (!string.IsNullOrWhiteSpace(surfaceRef))
        {
            var byId = workspace.Surfaces.FirstOrDefault(s => string.Equals(s.Surface.Id, surfaceRef, StringComparison.Ordinal));
            if (byId != null)
            {
                surface = byId;
                return true;
            }

            if (TryParseRefIndex(surfaceRef, "surface", out var surfaceRefIndex)
                && TryResolveCollectionIndex(surfaceRefIndex, workspace.Surfaces.Count, out var resolvedRefIndex))
            {
                surface = workspace.Surfaces[resolvedRefIndex];
                return true;
            }

            if (int.TryParse(surfaceRef, out var surfaceNumeric)
                && TryResolveCollectionIndex(surfaceNumeric, workspace.Surfaces.Count, out var resolvedNumericIndex))
            {
                surface = workspace.Surfaces[resolvedNumericIndex];
                return true;
            }

            error = $"Surface id/ref/index not found: {surfaceRef}";
            return false;
        }

        if (args.TryGetValue("surfaceName", out var surfaceName) && !string.IsNullOrWhiteSpace(surfaceName))
        {
            var byName = workspace.Surfaces.FirstOrDefault(s => string.Equals(s.Name, surfaceName, StringComparison.OrdinalIgnoreCase))
                ?? workspace.Surfaces.FirstOrDefault(s => s.Name.Contains(surfaceName, StringComparison.OrdinalIgnoreCase));
            if (byName == null)
            {
                error = $"Surface name not found: {surfaceName}";
                return false;
            }

            surface = byName;
            return true;
        }

        if (args.TryGetValue("surfaceIndex", out var surfaceIndexRaw)
            && int.TryParse(surfaceIndexRaw, out var surfaceIndex))
        {
            if (!TryResolveCollectionIndex(surfaceIndex, workspace.Surfaces.Count, out var resolvedIndex))
            {
                error = $"Surface index out of range: {surfaceIndex}";
                return false;
            }

            surface = workspace.Surfaces[resolvedIndex];
            return true;
        }

        return true;
    }

    private static bool TryResolveBrowserSurface(
        WorkspaceViewModel workspace,
        Dictionary<string, string> args,
        out SurfaceViewModel surface,
        out string error)
    {
        surface = null!;
        error = "";

        var browsers = workspace.Surfaces.Where(s => s.IsBrowserSurface).ToList();
        if (browsers.Count == 0)
        {
            error = "No browser surface available in workspace.";
            return false;
        }

        var browserRef = args.GetValueOrDefault("browser")
            ?? args.GetValueOrDefault("browserId")
            ?? args.GetValueOrDefault("surface")
            ?? args.GetValueOrDefault("surfaceId");

        if (!string.IsNullOrWhiteSpace(browserRef))
        {
            var byId = browsers.FirstOrDefault(s => string.Equals(s.Surface.Id, browserRef, StringComparison.Ordinal));
            if (byId != null)
            {
                surface = byId;
                return true;
            }

            if (TryParseRefIndex(browserRef, "browser", out var browserRefIndex)
                && TryResolveCollectionIndex(browserRefIndex, browsers.Count, out var resolvedRefIndex))
            {
                surface = browsers[resolvedRefIndex];
                return true;
            }

            if (int.TryParse(browserRef, out var browserNumeric)
                && TryResolveCollectionIndex(browserNumeric, browsers.Count, out var resolvedNumericIndex))
            {
                surface = browsers[resolvedNumericIndex];
                return true;
            }

            error = $"Browser id/ref/index not found: {browserRef}";
            return false;
        }

        if (args.TryGetValue("name", out var browserName) && !string.IsNullOrWhiteSpace(browserName))
        {
            var byName = browsers.FirstOrDefault(s => string.Equals(s.Name, browserName, StringComparison.OrdinalIgnoreCase))
                ?? browsers.FirstOrDefault(s => s.Name.Contains(browserName, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
            {
                surface = byName;
                return true;
            }
        }

        surface = workspace.SelectedSurface != null && workspace.SelectedSurface.IsBrowserSurface
            ? workspace.SelectedSurface
            : browsers[0];
        return true;
    }

    private static bool TryResolvePaneId(
        SurfaceViewModel surface,
        Dictionary<string, string> args,
        out string paneId,
        out int paneIndex,
        out string paneName,
        out string error)
    {
        paneId = "";
        paneIndex = -1;
        paneName = "";
        error = "";

        var panes = surface.RootNode.GetLeaves()
            .Select(l => l.PaneId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Select((id, idx) =>
            {
                surface.Surface.PaneCustomNames.TryGetValue(id, out var customName);
                return new
                {
                    Id = id,
                    Index = idx + 1,
                    Name = string.IsNullOrWhiteSpace(customName) ? $"Pane {idx + 1}" : customName!,
                    CustomName = customName ?? "",
                };
            })
            .ToList();

        if (panes.Count == 0)
        {
            error = "No panes available in surface.";
            return false;
        }

        string? target = null;

        if (args.TryGetValue("paneId", out var requestedPaneId) && !string.IsNullOrWhiteSpace(requestedPaneId))
        {
            target = panes.FirstOrDefault(p => string.Equals(p.Id, requestedPaneId, StringComparison.Ordinal))?.Id;
            if (target == null && TryParseRefIndex(requestedPaneId, "pane", out var paneRefIndex))
                target = panes.FirstOrDefault(p => p.Index == paneRefIndex)?.Id;
            if (target == null)
            {
                error = $"Pane id not found: {requestedPaneId}";
                return false;
            }
        }
        else if (args.TryGetValue("paneName", out var requestedPaneName) && !string.IsNullOrWhiteSpace(requestedPaneName))
        {
            target = panes.FirstOrDefault(p => string.Equals(p.CustomName, requestedPaneName, StringComparison.OrdinalIgnoreCase))?.Id
                ?? panes.FirstOrDefault(p => string.Equals(p.Name, requestedPaneName, StringComparison.OrdinalIgnoreCase))?.Id;
            if (target == null)
            {
                error = $"Pane name not found: {requestedPaneName}";
                return false;
            }
        }
        else if (args.TryGetValue("paneIndex", out var paneIndexRaw) && int.TryParse(paneIndexRaw, out var requestedIndex))
        {
            if (!TryResolveCollectionIndex(requestedIndex, panes.Count, out var resolvedIndex))
            {
                error = $"Pane index out of range: {requestedIndex}";
                return false;
            }

            target = panes[resolvedIndex].Id;
        }
        else
        {
            target = !string.IsNullOrWhiteSpace(surface.FocusedPaneId)
                ? panes.FirstOrDefault(p => string.Equals(p.Id, surface.FocusedPaneId, StringComparison.Ordinal))?.Id
                : null;

            target ??= panes[0].Id;
        }

        var pane = panes.First(p => string.Equals(p.Id, target, StringComparison.Ordinal));
        paneId = pane.Id;
        paneIndex = pane.Index;
        paneName = pane.Name;
        return true;
    }

    private static bool TryResolveCollectionIndex(int requested, int count, out int zeroBasedIndex)
    {
        zeroBasedIndex = -1;
        if (count <= 0)
            return false;

        if (requested >= 1 && requested <= count)
        {
            zeroBasedIndex = requested - 1;
            return true;
        }

        if (requested >= 0 && requested < count)
        {
            zeroBasedIndex = requested;
            return true;
        }

        return false;
    }

    private static string TailLines(string? text, int lines)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var split = text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        var tail = split.TakeLast(Math.Max(1, lines));
        return string.Join("\n", tail).TrimEnd();
    }

    private static string ResolveSubmitSequence(string? submitKey)
    {
        var key = (submitKey ?? "auto").Trim().ToLowerInvariant();

        if (key is "auto" or "")
            return "\r";

        return key switch
        {
            "enter" or "cr" or "ctrl+m" => "\r",
            "linefeed" or "lf" or "ctrl+j" => "\n",
            "crlf" => "\r\n",
            "none" => "",
            _ => "\r",
        };
    }

    private static bool ParseBoolArg(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static bool TryParseRefIndex(string raw, string prefix, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var normalizedPrefix = prefix + ":";
        if (!raw.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(raw[normalizedPrefix.Length..], out index);
    }

    private static Dictionary<string, string> ExtractPrefixedArgs(Dictionary<string, string> args, string prefix)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var normalizedPrefix = prefix + "";

        foreach (var (key, value) in args)
        {
            if (!key.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase) || key.Length == normalizedPrefix.Length)
                continue;

            var stripped = key[normalizedPrefix.Length..];
            var normalizedKey = char.ToLowerInvariant(stripped[0]) + stripped[1..];
            map[normalizedKey] = value;
        }

        return map;
    }
}
