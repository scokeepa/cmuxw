using Cmux.Core.Models;

namespace Cmux.Core.Services;

public sealed class ExplorerFileSystemService
{
    private readonly ExplorerPathPolicy _policy;

    public ExplorerFileSystemService(ExplorerPathPolicy policy)
    {
        _policy = policy;
    }

    public IEnumerable<ExplorerEntry> GetChildren(string directory)
    {
        var dir = _policy.NormalizePath(directory);
        if (string.IsNullOrWhiteSpace(dir) || !_policy.IsPathWithinAnyRoot(dir) || !Directory.Exists(dir))
            return [];

        var entries = new List<ExplorerEntry>();

        try
        {
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                var normalized = _policy.NormalizePath(d);
                if (!_policy.IsPathWithinAnyRoot(normalized))
                    continue;

                bool hasChildren = false;
                try
                {
                    hasChildren = Directory.EnumerateFileSystemEntries(normalized).Any();
                }
                catch
                {
                    hasChildren = false;
                }

                entries.Add(new ExplorerEntry
                {
                    Name = Path.GetFileName(normalized),
                    FullPath = normalized,
                    IsDirectory = true,
                    HasChildren = hasChildren,
                });
            }
        }
        catch
        {
            // Keep UI responsive even when a directory is inaccessible.
        }

        try
        {
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                var normalized = _policy.NormalizePath(f);
                if (!_policy.IsPathWithinAnyRoot(normalized))
                    continue;
                entries.Add(new ExplorerEntry
                {
                    Name = Path.GetFileName(normalized),
                    FullPath = normalized,
                    IsDirectory = false,
                    HasChildren = false,
                });
            }
        }
        catch
        {
            // Keep UI responsive even when a directory is inaccessible.
        }

        return entries
            .OrderBy(e => e.IsDirectory ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string CreateFile(string parentDirectory, string fileName)
    {
        if (!_policy.IsValidName(fileName))
            throw new InvalidOperationException("Invalid file name.");

        var parent = _policy.NormalizePath(parentDirectory);
        if (string.IsNullOrWhiteSpace(parent) || !_policy.IsPathWithinAnyRoot(parent) || !Directory.Exists(parent))
            throw new InvalidOperationException("Target directory is not valid.");

        var target = _policy.NormalizePath(Path.Combine(parent, fileName));
        if (!_policy.IsPathWithinAnyRoot(target))
            throw new InvalidOperationException("Target path is outside allowed roots.");
        if (File.Exists(target) || Directory.Exists(target))
            throw new InvalidOperationException("A file or folder with the same name already exists.");

        using var _ = File.Create(target);
        return target;
    }

    public string CreateFolder(string parentDirectory, string folderName)
    {
        if (!_policy.IsValidName(folderName))
            throw new InvalidOperationException("Invalid folder name.");

        var parent = _policy.NormalizePath(parentDirectory);
        if (string.IsNullOrWhiteSpace(parent) || !_policy.IsPathWithinAnyRoot(parent) || !Directory.Exists(parent))
            throw new InvalidOperationException("Target directory is not valid.");

        var target = _policy.NormalizePath(Path.Combine(parent, folderName));
        if (!_policy.IsPathWithinAnyRoot(target))
            throw new InvalidOperationException("Target path is outside allowed roots.");
        if (File.Exists(target) || Directory.Exists(target))
            throw new InvalidOperationException("A file or folder with the same name already exists.");

        Directory.CreateDirectory(target);
        return target;
    }

    public string Rename(string sourcePath, string newName)
    {
        if (!_policy.IsValidName(newName))
            throw new InvalidOperationException("Invalid name.");

        var src = _policy.NormalizePath(sourcePath);
        if (string.IsNullOrWhiteSpace(src) || !_policy.IsPathWithinAnyRoot(src))
            throw new InvalidOperationException("Source path is not valid.");

        var parent = Path.GetDirectoryName(src);
        if (string.IsNullOrWhiteSpace(parent))
            throw new InvalidOperationException("Cannot rename this path.");

        var target = _policy.NormalizePath(Path.Combine(parent, newName));
        if (!_policy.IsPathWithinAnyRoot(target))
            throw new InvalidOperationException("Target path is outside allowed roots.");
        if (File.Exists(target) || Directory.Exists(target))
            throw new InvalidOperationException("A file or folder with the same name already exists.");

        if (Directory.Exists(src))
            Directory.Move(src, target);
        else if (File.Exists(src))
            File.Move(src, target);
        else
            throw new InvalidOperationException("Source path does not exist.");

        return target;
    }

    public string Move(string sourcePath, string targetDirectory)
    {
        var src = _policy.NormalizePath(sourcePath);
        var dstDir = _policy.NormalizePath(targetDirectory);
        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(dstDir))
            throw new InvalidOperationException("Path is not valid.");
        if (!_policy.IsPathWithinAnyRoot(src) || !_policy.IsPathWithinAnyRoot(dstDir))
            throw new InvalidOperationException("Source or target path is outside allowed roots.");
        if (!Directory.Exists(dstDir))
            throw new InvalidOperationException("Target directory does not exist.");

        var name = Path.GetFileName(src);
        var dst = _policy.NormalizePath(Path.Combine(dstDir, name));
        if (src.Equals(dst, StringComparison.OrdinalIgnoreCase))
            return dst;
        if (File.Exists(dst) || Directory.Exists(dst))
            throw new InvalidOperationException("A file or folder with the same name already exists.");

        if (Directory.Exists(src))
            Directory.Move(src, dst);
        else if (File.Exists(src))
            File.Move(src, dst);
        else
            throw new InvalidOperationException("Source path does not exist.");

        return dst;
    }

    public void Delete(string sourcePath)
    {
        var src = _policy.NormalizePath(sourcePath);
        if (string.IsNullOrWhiteSpace(src) || !_policy.IsPathWithinAnyRoot(src))
            throw new InvalidOperationException("Path is not valid.");

        if (Directory.Exists(src))
            Directory.Delete(src, recursive: true);
        else if (File.Exists(src))
            File.Delete(src);
        else
            throw new InvalidOperationException("Path does not exist.");
    }
}
