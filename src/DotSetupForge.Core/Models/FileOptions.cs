namespace DotSetupForge.Core.Models;

/// <summary>文件规则选项（默认规则不硬编码在引擎里，全部可配置）。</summary>
public record FileOptions
{
    /// <summary>默认包含模式。</summary>
    public List<string> Include { get; init; } =
    [
        "*.exe",
        "*.dll",
        "*.json",
        "*.config",
        "runtimes/**",
    ];

    /// <summary>默认排除模式。</summary>
    public List<string> Exclude { get; init; } =
    [
        "*.pdb",
        "*.log",
        "Logs/**",
        "obj/**",
    ];

    /// <summary>
    /// 文件树中被单独选中的目录路径。它仅保存界面节点状态，不参与文件筛选：
    /// 允许“目录选中、子文件未选中”的显示状态在重新打开项目后保持不变。
    /// </summary>
    public List<string> SelectedDirectoryPaths { get; init; } = [];
}
