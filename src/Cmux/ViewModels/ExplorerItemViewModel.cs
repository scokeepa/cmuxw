using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Cmux.Core.Models;
using Cmux.Core.Services;

namespace Cmux.ViewModels;

public partial class ExplorerItemViewModel : ObservableObject
{
    private readonly ExplorerFileSystemService _fs;
    private readonly bool _isPlaceholder;

    public ExplorerItemViewModel(
        ExplorerFileSystemService fs,
        string rootId,
        string fullPath,
        bool isDirectory,
        bool hasChildren,
        string? displayName = null,
        ExplorerItemViewModel? parent = null)
    {
        _fs = fs;
        RootId = rootId;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        _isExpanded = false;
        Parent = parent;
        Name = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : displayName;
        _isPlaceholder = false;

        if (IsDirectory && hasChildren)
            Children.Add(CreatePlaceholder(fs, rootId, this));
    }

    private ExplorerItemViewModel(
        ExplorerFileSystemService fs,
        string rootId,
        string fullPath,
        ExplorerItemViewModel parent)
    {
        _fs = fs;
        RootId = rootId;
        FullPath = fullPath;
        IsDirectory = false;
        Parent = parent;
        Name = "Loading...";
        _isPlaceholder = true;
    }

    private static ExplorerItemViewModel CreatePlaceholder(
        ExplorerFileSystemService fs,
        string rootId,
        ExplorerItemViewModel parent)
        => new(fs, rootId, parent.FullPath + "\\.", parent);

    public string RootId { get; }

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _fullPath = "";

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoading;

    public ExplorerItemViewModel? Parent { get; private set; }

    public ObservableCollection<ExplorerItemViewModel> Children { get; } = [];

    public bool IsRoot => Parent == null;
    public bool IsPlaceholder => _isPlaceholder;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? FullPath : Name;
    public string Glyph => IsDirectory ? "\uE838" : "\uE8A5";

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && IsDirectory)
            _ = EnsureChildrenLoadedAsync(forceReload: false);
    }

    public async Task EnsureChildrenLoadedAsync(bool forceReload)
    {
        if (!IsDirectory || IsLoading)
            return;

        if (!forceReload && Children.Count > 0 && Children.All(c => !c.IsPlaceholder))
            return;

        IsLoading = true;
        try
        {
            await Task.Run(() =>
            {
                var childEntries = _fs.GetChildren(FullPath).ToList();
                var children = childEntries.Select(e =>
                    new ExplorerItemViewModel(_fs, RootId, e.FullPath, e.IsDirectory, e.HasChildren, e.Name, this)).ToList();

                App.Current.Dispatcher.Invoke(() =>
                {
                    Children.Clear();
                    foreach (var child in children)
                        Children.Add(child);
                });
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    public IEnumerable<ExplorerItemViewModel> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children.Where(c => !c.IsPlaceholder))
        {
            foreach (var next in child.DescendantsAndSelf())
                yield return next;
        }
    }

    public void MarkParent(ExplorerItemViewModel? parent)
    {
        Parent = parent;
    }
}
