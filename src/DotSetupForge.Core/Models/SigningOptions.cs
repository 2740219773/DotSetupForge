namespace DotSetupForge.Core.Models;

/// <summary>签名选项。</summary>
public record SigningOptions
{
    /// <summary>是否启用签名。</summary>
    public bool Enabled { get; init; }

    /// <summary>PFX 证书路径（密码绝不写入配置，构建时通过环境变量/凭据管理器提供）。</summary>
    public string CertificatePath { get; init; } = string.Empty;

    /// <summary>时间戳服务器。</summary>
    public string TimestampServer { get; init; } = "http://timestamp.digicert.com";
}
