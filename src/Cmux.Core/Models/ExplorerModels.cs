namespace Cmux.Core.Models;

public sealed class ExplorerRootConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Path { get; set; } = "";
    public string? DisplayName { get; set; }
}

public sealed class WorkspaceExplorerState
{
    public List<ExplorerRootConfig> Roots { get; set; } = [];
    public string? SelectedPath { get; set; }
    public List<string> ExpandedPaths { get; set; } = [];
}

public sealed class ExplorerEntry
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public bool IsDirectory { get; init; }
    public bool HasChildren { get; init; }
}
