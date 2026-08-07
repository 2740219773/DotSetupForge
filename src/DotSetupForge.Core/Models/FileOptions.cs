namespace DotSetupForge.Core.Models;

/// <summary>文件规则选项（默认规则不硬编码在引擎里，全部可配置）。</summary>
public sealed class FileOptions
{
    /// <summary>默认包含模式。</summary>
    public List<string> Include { get; set; } =
    [
        "*.exe",
        "*.dll",
        "*.json",
        "*.config",
        "runtimes/**",
    ];

    /// <summary>默认排除模式。</summary>
    public List<string> Exclude { get; set; } =
    [
        "*.pdb",
        "*.log",
        "Logs/**",
        "obj/**",
    ];
}
