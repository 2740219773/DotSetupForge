namespace DotSetupForge.Core.Packaging;

/// <summary>文件规则动作。</summary>
public enum FileRuleAction
{
    Include,
    Exclude,
}

/// <summary>安装位置。</summary>
public enum InstallLocation
{
    /// <summary>应用目录（{app}）。</summary>
    ApplicationDirectory,

    /// <summary>ProgramData（{commonappdata}）。</summary>
    ProgramData,

    /// <summary>当前用户 LocalAppData（{localappdata}）。</summary>
    LocalAppData,

    /// <summary>当前用户 AppData（{userappdata}）。</summary>
    AppData,

    /// <summary>自定义路径。</summary>
    Custom,
}

/// <summary>升级策略。</summary>
public enum UpgradePolicy
{
    /// <summary>每次升级覆盖。</summary>
    OverwriteAlways,

    /// <summary>升级时保留现有文件（如 appsettings.json）。</summary>
    PreserveExisting,

    /// <summary>仅缺失时安装。</summary>
    InstallIfMissing,

    /// <summary>升级时删除（临时文件等）。</summary>
    DeleteOnUpgrade,
}

/// <summary>卸载策略。</summary>
public enum UninstallPolicy
{
    /// <summary>卸载时删除。</summary>
    Delete,

    /// <summary>卸载时保留（用户数据/配置）。</summary>
    NeverUninstall,
}

/// <summary>统一文件规则：Pattern + Action，可选位置与升级策略覆盖。</summary>
public sealed record FileRule(
    string Pattern,
    FileRuleAction Action,
    InstallLocation? InstallLocation = null,
    UpgradePolicy? UpgradePolicy = null);
