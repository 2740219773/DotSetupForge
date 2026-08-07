namespace DotSetupForge.Core.Models;

/// <summary>产品信息。</summary>
public record ProductInfo
{
    /// <summary>AppId：首次创建生成 GUID，之后禁止自动改变（升级时保持不变）。</summary>
    public Guid AppId { get; init; } = Guid.NewGuid();

    /// <summary>产品名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>产品版本（SemVer）。</summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>发布者。</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>主程序文件名，例如 RHCVP.exe。</summary>
    public string MainExecutable { get; init; } = string.Empty;
}
