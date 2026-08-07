namespace DotSetupForge.Core.Models;

/// <summary>安装范围。</summary>
public enum InstallScope
{
    /// <summary>所有用户（Program Files，需要管理员权限）。</summary>
    Machine,

    /// <summary>当前用户（LocalAppData\Programs）。</summary>
    User,
}

/// <summary>安装器选项。</summary>
public sealed class InstallerOptions
{
    /// <summary>安装范围。</summary>
    public InstallScope Scope { get; set; } = InstallScope.Machine;

    /// <summary>安装目录模板，如 {autopf}\{Publisher}\{Name}。</summary>
    public string InstallDirectory { get; set; } = string.Empty;

    /// <summary>创建桌面快捷方式。</summary>
    public bool CreateDesktopShortcut { get; set; } = true;

    /// <summary>创建开始菜单快捷方式。</summary>
    public bool CreateStartMenuShortcut { get; set; } = true;

    /// <summary>安装完成后允许运行程序。</summary>
    public bool LaunchAfterInstall { get; set; } = false;

    /// <summary>支持覆盖升级（保持 AppId 不变）。</summary>
    public bool AllowUpgrade { get; set; } = true;
}
