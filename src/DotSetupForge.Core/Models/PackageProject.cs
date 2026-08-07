namespace DotSetupForge.Core.Models;

/// <summary>打包项目聚合根：代表一个完整的打包项目。</summary>
public record PackageProject
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>配置文件格式版本，用于以后配置迁移。</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>产品信息（名称、版本、发布者、AppId、主程序）。</summary>
    public ProductInfo Product { get; init; } = new();

    /// <summary>源（程序目录或 .csproj）。</summary>
    public SourceInfo Source { get; init; } = new();

    /// <summary>运行时要求。</summary>
    public RuntimeInfo Runtime { get; init; } = new();

    /// <summary>文件规则。</summary>
    public FileOptions Files { get; init; } = new();

    /// <summary>安装器选项。</summary>
    public InstallerOptions Installer { get; init; } = new();

    /// <summary>签名选项。</summary>
    public SigningOptions Signing { get; init; } = new();

    /// <summary>输出选项。</summary>
    public OutputOptions Output { get; init; } = new();
}
