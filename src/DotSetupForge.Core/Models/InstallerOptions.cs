namespace DotSetupForge.Core.Models;

/// <summary>安装范围。</summary>
public enum InstallScope
{
    /// <summary>所有用户（Program Files，需要管理员权限）。</summary>
    Machine,

    /// <summary>当前用户（LocalAppData\Programs）。</summary>
    User,
}

/// <summary>Inno Setup 安装向导内置主题。</summary>
public enum InstallerWizardTheme
{
    Stellar,
    Slate,
    Zircon,
    ModernLight,
}

/// <summary>安装器选项。</summary>
public record InstallerOptions
{
    /// <summary>安装器主题；旧项目缺省使用深蓝 Stellar。</summary>
    public InstallerWizardTheme WizardTheme { get; init; } = InstallerWizardTheme.Stellar;
    /// <summary>安装范围。</summary>
    public InstallScope Scope { get; init; } = InstallScope.Machine;

    /// <summary>安装目录；默认优先 D:\Apps\{Publisher}\{Name}，也可使用 Inno 常量模板。</summary>
    public string InstallDirectory { get; init; } = string.Empty;

    /// <summary>创建桌面快捷方式。</summary>
    public bool CreateDesktopShortcut { get; init; } = true;

    /// <summary>创建开始菜单快捷方式。</summary>
    public bool CreateStartMenuShortcut { get; init; } = true;

    /// <summary>安装完成后允许运行程序。</summary>
    public bool LaunchAfterInstall { get; init; } = false;

    /// <summary>安装程序 EXE 的 ICO 图标文件路径；为空时使用 Inno Setup 默认图标。</summary>
    public string SetupIconPath { get; init; } = string.Empty;

    /// <summary>安装向导右上角品牌图（PNG 或 BMP）；为空时不显示 Inno Setup 的默认纸箱图标。</summary>
    public string WizardSmallImagePath { get; init; } = string.Empty;

    /// <summary>安装向导欢迎/完成页左侧品牌图（PNG 或 BMP）；为空时不额外显示欢迎页。</summary>
    public string WizardImagePath { get; init; } = string.Empty;

    /// <summary>支持覆盖升级（保持 AppId 不变）。</summary>
    public bool AllowUpgrade { get; init; } = true;
}
