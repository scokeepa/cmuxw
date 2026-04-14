using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Cmux.Core.Models;
using Cmux.Core.Services;

namespace Cmux.ViewModels;

public partial class ExplorerViewModel : ObservableObject
{
    private readonly Workspace _workspace;
    private readonly ExplorerPathPolicy _policy = new();
    private readonly ExplorerFileSystemService _fs;

    [ObservableProperty]
    private ObservableCollection<ExplorerItemViewModel> _roots = [];

    [ObservableProperty]
    private ExplorerItemViewModel? _selectedItem;

    [ObservableProperty]
    private string _filterText = "";

    public ExplorerViewModel(Workspace workspace)
    {
        _workspace = workspace;
        _fs = new ExplorerFileSystemService(_policy);
        EnsureDefaultRoot();
        ReloadRoots();
    }

    public IReadOnlyList<ExplorerRootConfig> RootConfigs => _workspace.ExplorerState.Roots;

    private void EnsureDefaultRoot()
    {
        _workspace.ExplorerState ??= new WorkspaceExplorerState();
        if (_workspace.ExplorerState.Roots.Count > 0)
            return;

        if (!string.IsNullOrWhiteSpace(_workspace.WorkingDirectory) && Directory.Exists(_workspace.WorkingDirectory))
        {
            var rootName = Path.GetFileName(
                _workspace.WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            _workspace.ExplorerState.Roots.Add(new ExplorerRootConfig
            {
                Path = _workspace.WorkingDirectory,
                DisplayName = string.IsNullOrWhiteSpace(rootName) ? _workspace.WorkingDirectory : rootName,
            });
        }
    }

    public void ReloadRoots()
    {
        _policy.SetRoots(_workspace.ExplorerState.Roots.Select(r => r.Path));

        var restoredExpanded = new HashSet<string>(
            _workspace.ExplorerState.ExpandedPaths.Select(_policy.NormalizePath),
            StringComparer.OrdinalIgnoreCase);
        var selectedPath = _policy.NormalizePath(_workspace.ExplorerState.SelectedPath);

        var nextRoots = new List<ExplorerItemViewModel>();
        foreach (var root in _workspace.ExplorerState.Roots)
        {
            var path = _policy.NormalizePath(root.Path);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                continue;

            var displayName = string.IsNullOrWhiteSpace(root.DisplayName)
                ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : root.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = path;

            bool hasChildren = false;
            try { hasChildren = Directory.EnumerateFileSystemEntries(path).Any(); } catch { }

            var node = new ExplorerItemViewModel(_fs, root.Id, path, true, hasChildren, displayName, null);
            nextRoots.Add(node);
        }

        Roots = new ObservableCollection<ExplorerItemViewModel>(nextRoots);
        _ = RestoreExpandedStateAsync(restoredExpanded, selectedPath);
    }

    public bool TryAddRoot(string directoryPath, out string error)
    {
        error = "";
        var normalized = _policy.NormalizePath(directoryPath);
        if (string.IsNullOrWhiteSpace(normalized) || !Directory.Exists(normalized))
        {
            error = "Directory does not exist.";
            return false;
        }

        if (_workspace.ExplorerState.Roots.Any(r => _policy.NormalizePath(r.Path).Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Root already exists.";
            return false;
        }

        _workspace.ExplorerState.Roots.Add(new ExplorerRootConfig
        {
            Path = normalized,
            DisplayName = Path.GetFileName(normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
        });
        ReloadRoots();
        return true;
    }

    public void RemoveRoot(ExplorerItemViewModel rootNode)
    {
        if (!rootNode.IsRoot)
            return;

        var target = _workspace.ExplorerState.Roots
            .FirstOrDefault(r => r.Id == rootNode.RootId
                                 || _policy.NormalizePath(r.Path).Equals(_policy.NormalizePath(rootNode.FullPath), StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return;

        _workspace.ExplorerState.Roots.Remove(target);
        ReloadRoots();
    }

    public void MoveRootBefore(ExplorerItemViewModel sourceRoot, ExplorerItemViewModel targetRoot)
    {
        if (!sourceRoot.IsRoot || !targetRoot.IsRoot || sourceRoot.RootId == targetRoot.RootId)
            return;

        var sourceIndex = _workspace.ExplorerState.Roots.FindIndex(r => r.Id == sourceRoot.RootId);
        var targetIndex = _workspace.ExplorerState.Roots.FindIndex(r => r.Id == targetRoot.RootId);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return;

        var item = _workspace.ExplorerState.Roots[sourceIndex];
        _workspace.ExplorerState.Roots.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
            targetIndex--;
        _workspace.ExplorerState.Roots.Insert(targetIndex, item);
        ReloadRoots();
    }

    public async Task RefreshNodeAsync(ExplorerItemViewModel? node)
    {
        if (node == null)
        {
            ReloadRoots();
            return;
        }

        await node.EnsureChildrenLoadedAsync(forceReload: true);
    }

    public bool TryCreateFile(ExplorerItemViewModel? target, string name, out string path, out string error)
    {
        path = "";
        error = "";
        try
        {
            var parent = ResolveParentDirectory(target);
            path = _fs.CreateFile(parent, name);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryCreateFolder(ExplorerItemViewModel? target, string name, out string path, out string error)
    {
        path = "";
        error = "";
        try
        {
            var parent = ResolveParentDirectory(target);
            path = _fs.CreateFolder(parent, name);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryRename(ExplorerItemViewModel node, string newName, out string nextPath, out string error)
    {
        nextPath = "";
        error = "";
        try
        {
            nextPath = _fs.Rename(node.FullPath, newName);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryDelete(ExplorerItemViewModel node, out string error)
    {
        error = "";
        try
        {
            _fs.Delete(node.FullPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryMove(ExplorerItemViewModel source, ExplorerItemViewModel destination, out string newPath, out string error)
    {
        newPath = "";
        error = "";
        try
        {
            var targetDir = destination.IsDirectory
                ? destination.FullPath
                : Path.GetDirectoryName(destination.FullPath) ?? "";
            newPath = _fs.Move(source.FullPath, targetDir);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void CaptureStateForPersistence()
    {
        _workspace.ExplorerState.SelectedPath = SelectedItem?.FullPath;
        _workspace.ExplorerState.ExpandedPaths = Roots
            .SelectMany(r => r.DescendantsAndSelf())
            .Where(n => n.IsDirectory && n.IsExpanded && !n.IsPlaceholder)
            .Select(n => n.FullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IEnumerable<ExplorerItemViewModel> FilteredChildren(ExplorerItemViewModel node)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            return node.Children;

        var q = FilterText.Trim();
        return node.Children.Where(c =>
            c.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || c.FullPath.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveParentDirectory(ExplorerItemViewModel? node)
    {
        if (node == null)
            return _workspace.ExplorerState.Roots.FirstOrDefault()?.Path ?? "";
        if (node.IsDirectory)
            return node.FullPath;
        return Path.GetDirectoryName(node.FullPath) ?? "";
    }

    private async Task RestoreExpandedStateAsync(HashSet<string> expandedPaths, string selectedPath)
    {
        foreach (var root in Roots)
        {
            if (expandedPaths.Contains(_policy.NormalizePath(root.FullPath)))
            {
                root.IsExpanded = true;
                await root.EnsureChildrenLoadedAsync(forceReload: false);
            }

            await ExpandMatchingDescendantsAsync(root, expandedPaths);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                var selected = root.DescendantsAndSelf()
                    .FirstOrDefault(n => _policy.NormalizePath(n.FullPath).Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
                if (selected != null)
                    SelectedItem = selected;
            }
        }
    }

    private async Task ExpandMatchingDescendantsAsync(ExplorerItemViewModel node, HashSet<string> expandedPaths)
    {
        await node.EnsureChildrenLoadedAsync(forceReload: false);
        foreach (var child in node.Children.Where(c => c.IsDirectory && !c.IsPlaceholder))
        {
            if (expandedPaths.Contains(_policy.NormalizePath(child.FullPath)))
            {
                child.IsExpanded = true;
                await child.EnsureChildrenLoadedAsync(forceReload: false);
            }

            await ExpandMatchingDescendantsAsync(child, expandedPaths);
        }
    }
}
