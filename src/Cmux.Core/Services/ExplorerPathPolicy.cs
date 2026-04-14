using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Cmux.Core.Services;

/// <summary>
/// Centralized path safety rules for Explorer operations.
/// </summary>
public sealed class ExplorerPathPolicy
{
    private readonly ObservableCollection<string> _allowedRoots = [];

    public IReadOnlyCollection<string> AllowedRoots => _allowedRoots;

    public void SetRoots(IEnumerable<string> roots)
    {
        _allowedRoots.Clear();
        foreach (var root in roots)
        {
            var normalized = NormalizeDirectory(root);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;
            if (_allowedRoots.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                continue;
            _allowedRoots.Add(normalized);
        }
    }

    public bool IsPathWithinAnyRoot(string? path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return _allowedRoots.Any(root => IsChildOrSame(normalized, root));
    }

    public string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return "";
        }
    }

    public string NormalizeDirectory(string? path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            return "";

        return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;
        if (name is "." or "..")
            return false;
        return !Regex.IsMatch(name, @"^\s+$");
    }

    private static bool IsChildOrSame(string path, string root)
    {
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return false;

        var separatorIndex = root.Length;
        return separatorIndex < path.Length
               && (path[separatorIndex] == Path.DirectorySeparatorChar
                   || path[separatorIndex] == Path.AltDirectorySeparatorChar);
    }
}
