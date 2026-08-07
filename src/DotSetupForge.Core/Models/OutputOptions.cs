namespace DotSetupForge.Core.Models;

/// <summary>输出选项。</summary>
public sealed class OutputOptions
{
    /// <summary>输出目录。</summary>
    public string Directory { get; set; } = "./dist";

    /// <summary>输出文件名模板，如 {ProductName}_Setup_{Version}.exe。</summary>
    public string FileName { get; set; } = "{ProductName}_Setup_{Version}.exe";
}
