namespace DotSetupForge.Core.Models;

/// <summary>源类型。</summary>
public enum SourceType
{
    /// <summary>程序发布目录（推荐 publish 目录）。</summary>
    Directory,

    /// <summary>.csproj / .sln 源码项目。</summary>
    Project,
}

/// <summary>打包源信息。</summary>
public sealed class SourceInfo
{
    /// <summary>源类型。</summary>
    public SourceType Type { get; set; } = SourceType.Directory;

    /// <summary>源路径：目录或项目文件。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>构建配置（Project 源时使用），例如 Release。</summary>
    public string Configuration { get; set; } = "Release";
}
