namespace DotSetupForge.Core.Models;

/// <summary>Runtime 家族。</summary>
public enum RuntimeFamily
{
    /// <summary>Microsoft.NETCore.App → .NET Runtime。</summary>
    DotNet,

    /// <summary>Microsoft.WindowsDesktop.App → .NET Desktop Runtime。</summary>
    WindowsDesktop,

    /// <summary>Microsoft.AspNetCore.App → ASP.NET Core Runtime。</summary>
    AspNetCore,
}

/// <summary>目标架构。</summary>
public enum TargetArchitecture
{
    X86,
    X64,
    Arm64,
    AnyCpu,
}

/// <summary>Runtime 部署模式。</summary>
public enum RuntimeDeploymentMode
{
    /// <summary>智能离线：Runtime 内嵌 Setup，目标机已有则跳过。</summary>
    SmartOffline,

    /// <summary>在线部署：安装时从 Microsoft 下载。</summary>
    Online,

    /// <summary>Self-contained：不安装系统 Runtime。</summary>
    SelfContained,

    /// <summary>不处理 Runtime。</summary>
    None,
}

/// <summary>运行时要求。</summary>
public sealed class RuntimeInfo
{
    /// <summary>Runtime 家族。</summary>
    public RuntimeFamily Family { get; set; } = RuntimeFamily.WindowsDesktop;

    /// <summary>主版本号（如 10.0）。</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>目标架构。</summary>
    public TargetArchitecture Architecture { get; set; } = TargetArchitecture.X64;

    /// <summary>部署模式。</summary>
    public RuntimeDeploymentMode Mode { get; set; } = RuntimeDeploymentMode.SmartOffline;

    /// <summary>是否自动检测（分析时自动填充）。</summary>
    public bool AutoDetect { get; set; } = true;
}
