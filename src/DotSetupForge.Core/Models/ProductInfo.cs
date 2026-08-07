namespace DotSetupForge.Core.Models;

/// <summary>产品信息。</summary>
public sealed class ProductInfo
{
    /// <summary>AppId：首次创建生成 GUID，之后禁止自动改变（升级时保持不变）。</summary>
    public Guid AppId { get; set; } = Guid.NewGuid();

    /// <summary>产品名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>产品版本（SemVer）。</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>发布者。</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>主程序文件名，例如 RHCVP.exe。</summary>
    public string MainExecutable { get; set; } = string.Empty;
}
