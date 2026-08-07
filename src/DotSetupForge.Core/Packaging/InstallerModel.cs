using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;

namespace DotSetupForge.Core.Packaging;

/// <summary>产品模型（安装器视图）。</summary>
public sealed record ProductModel(
    Guid AppId,
    string Name,
    string Version,
    string Publisher,
    string MainExecutable);

/// <summary>安装文件条目。</summary>
public sealed record InstallerFile(
    string SourcePath,
    string SourceRelativePath,
    long Size,
    FileCategory Category,
    InstallLocation InstallLocation,
    string? TargetSubDirectory,
    UpgradePolicy UpgradePolicy,
    UninstallPolicy UninstallPolicy);

/// <summary>空目录（如 {app}\Data）。</summary>
public sealed record InstallerDirectory(
    InstallLocation Location,
    string? TargetSubDirectory,
    UninstallPolicy UninstallPolicy);

/// <summary>快捷方式。</summary>
public sealed record ShortcutModel(
    string Name,
    string TargetRelativePath,
    string? IconRelativePath,
    bool Desktop,
    bool StartMenu);

/// <summary>前置依赖。</summary>
public sealed record PrerequisiteModel(
    string Id,
    string Name,
    string Version,
    TargetArchitecture Architecture,
    string InstallerFileName,
    string InstallArguments,
    IReadOnlyList<int> SuccessExitCodes,
    IReadOnlyList<int> RebootExitCodes);

/// <summary>注册表条目。</summary>
public sealed record RegistryEntryModel(
    string RootKey,
    string SubKey,
    string ValueName,
    string Value);

/// <summary>升级模型。</summary>
public sealed record UpgradeModel(bool Enabled, string? RunningProcessName);

/// <summary>卸载模型。</summary>
public sealed record UninstallModel(bool KeepUserData, bool KeepLogs);

/// <summary>
/// 安装器统一模型：Inno Setup 生成模块只认本模型，不再接触 ApplicationAnalyzer。
/// </summary>
public sealed record InstallerModel
{
    public ProductModel Product { get; init; } = new(Guid.NewGuid(), string.Empty, "1.0.0", string.Empty, string.Empty);

    public InstallScope Scope { get; init; } = InstallScope.Machine;

    /// <summary>默认安装目录，如 {autopf}\{Publisher}\{Name}。</summary>
    public string InstallDirectory { get; init; } = string.Empty;

    public string MainExecutable { get; init; } = string.Empty;

    public IReadOnlyList<InstallerFile> Files { get; init; } = [];

    public IReadOnlyList<InstallerDirectory> Directories { get; init; } = [];

    public IReadOnlyList<ShortcutModel> Shortcuts { get; init; } = [];

    public IReadOnlyList<PrerequisiteModel> Prerequisites { get; init; } = [];

    public IReadOnlyList<RegistryEntryModel> RegistryEntries { get; init; } = [];

    public UpgradeModel Upgrade { get; init; } = new(false, null);

    public UninstallModel Uninstall { get; init; } = new(false, false);

    public SigningOptions Signing { get; init; } = new();
}
