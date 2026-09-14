using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotSetupForge.Application.Runtime;
using DotSetupForge.Core.Models;
using DotSetupForge.Core.Runtime;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.Services;

namespace DotSetupForge.UI.ViewModels;

/// <summary>运行环境页：部署模式 + Runtime 缓存状态 + 下载。</summary>
public partial class RuntimePageViewModel : ObservableObject, IProjectPageViewModel
{
    private readonly IDialogService _dialogs;
    private readonly RuntimeService _runtime = new();
    private EditableProject? _project;

    public RuntimePageViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public EditableProject? Project => _project;

    /// <summary>部署模式选项。</summary>
    public IReadOnlyList<DeploymentModeOption> DeploymentModes { get; } =
    [
        new("SmartOffline", "智能离线部署", "Runtime 随安装包一起发布，目标机已有则自动跳过（推荐）", true),
        new("Online", "在线部署", "安装包不包含 Runtime，安装时从 Microsoft 下载", false),
        new("SelfContained", "Self-contained", "程序自包含运行时，不安装系统 Runtime", false),
        new("None", "不处理 Runtime", "目标机已统一部署环境，不包含任何 Runtime", true),
    ];

    // ---- 缓存状态 ----

    [ObservableProperty]
    private string _cacheStatus = "尚未检查";

    [ObservableProperty]
    private bool _cacheAvailable;

    [ObservableProperty]
    private string _cachedFileName = string.Empty;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadProgressText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>下载操作按钮的文字，缓存已可用时明确提示会复用缓存。</summary>
    public string DownloadButtonText => CacheAvailable ? "使用缓存" : "下载 Runtime";

    /// <summary>旧 .NET Framework 使用 Bootstrapper 定义与独立离线缓存。</summary>
    public bool IsLegacyFramework => IsLegacyFrameworkRuntime(
        _project?.RuntimeFamily,
        _project?.RuntimeVersion);

    /// <summary>
    /// 现代 .NET 从 5.0 开始；旧项目遗留的 DotNet + 4.x 配置必须按 .NET Framework 处理，
    /// 否则会错误请求不存在的现代 Runtime 元数据。
    /// </summary>
    internal static bool IsLegacyFrameworkRuntime(string? family, string? version) =>
        string.Equals(family, nameof(RuntimeFamily.NetFramework), StringComparison.Ordinal) ||
        (string.Equals(family, nameof(RuntimeFamily.DotNet), StringComparison.Ordinal) &&
         Version.TryParse(version, out var parsed) && parsed.Major == 4);

    public string RuntimeFamilyDisplay => IsLegacyFramework
        ? ".NET Framework（机器级）"
        : _project?.RuntimeFamily ?? "—";

    public string RuntimeArchitectureDisplay => IsLegacyFramework
        ? "不区分 x86/x64（4.x 就地升级）"
        : _project?.RuntimeArchitecture ?? "—";

    public string LegacyRequirementText => IsLegacyFramework
        ? $"检测到 .NET Framework {_project?.RuntimeVersion ?? "—"}；安装器按 Full\\Release 最低值检测。"
        : string.Empty;

    /// <summary>仅在自动扫描不到兼容的 ENU/CHS 安装器时显示手动选择入口。</summary>
    public bool NeedsLegacyInstallerSelection => IsLegacyFramework && !CacheAvailable;

    public void Bind(EditableProject project)
    {
        _project = project;
        if (IsLegacyFramework)
        {
            _project.RuntimeFamily = nameof(RuntimeFamily.NetFramework);
            _project.RuntimeMode = nameof(RuntimeDeploymentMode.SmartOffline);
        }
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(IsLegacyFramework));
        OnPropertyChanged(nameof(RuntimeFamilyDisplay));
        OnPropertyChanged(nameof(RuntimeArchitectureDisplay));
        OnPropertyChanged(nameof(LegacyRequirementText));
        OnPropertyChanged(nameof(NeedsLegacyInstallerSelection));
        CheckCacheCommand.Execute(null);
    }

    private RuntimeRequirement? BuildRequirement()
    {
        if (_project is null)
        {
            return null;
        }

        if (!Enum.TryParse<RuntimeFamily>(_project.RuntimeFamily, out var family))
        {
            family = RuntimeFamily.WindowsDesktop;
        }

        if (!Enum.TryParse<TargetArchitecture>(_project.RuntimeArchitecture, out var arch))
        {
            arch = TargetArchitecture.X64;
        }

        return new RuntimeRequirement(family, _project.RuntimeVersion, arch);
    }

    [RelayCommand]
    private void CheckCache()
    {
        if (IsLegacyFramework)
        {
            CheckLegacyFrameworkCache();
            return;
        }

        var requirement = BuildRequirement();
        if (requirement is null || string.IsNullOrEmpty(requirement.Version))
        {
            CacheStatus = "缺少 Runtime 版本信息（请先在「应用」页完成分析）";
            CacheAvailable = false;
            OnPropertyChanged(nameof(DownloadButtonText));
            return;
        }

        var cached = _runtime.ListCached();
        var match = cached.FirstOrDefault(c =>
            c.Family == requirement.Family &&
            c.Architecture == requirement.Architecture &&
            IsVersionCompatible(c.Version, requirement.Version));

        if (match is not null)
        {
            CacheStatus = $"已缓存：{match.Family} {match.Version} {match.Architecture}";
            CachedFileName = match.FileName;
            CacheAvailable = true;
        }
        else
        {
            CacheStatus = $"缓存中未找到 {requirement.Family} {requirement.Version} {requirement.Architecture}";
            CachedFileName = string.Empty;
            CacheAvailable = false;
        }

        OnPropertyChanged(nameof(DownloadButtonText));
    }

    private void CheckLegacyFrameworkCache()
    {
        var version = _project?.RuntimeVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            CacheStatus = "缺少 .NET Framework 版本信息（请先在“应用”页完成分析）";
            CacheAvailable = false;
            OnPropertyChanged(nameof(NeedsLegacyInstallerSelection));
            return;
        }

        var ensured = _runtime.EnsureLegacyFramework(version);
        var package = ensured.Package;
        if (package is null)
        {
            CacheStatus = version.StartsWith("3.5", StringComparison.Ordinal)
                ? ".NET Framework 3.5 SP1 需要完整多文件 Bootstrapper 载荷，当前不支持单 EXE 离线嵌入。"
                : $"ClickOnce Bootstrapper 中未找到 .NET Framework {version} 的可用离线包定义。";
            CachedFileName = string.Empty;
            CacheAvailable = false;
            OnPropertyChanged(nameof(NeedsLegacyInstallerSelection));
            return;
        }

        var cached = ensured.Cached;
        var cachedPackage = cached is null ? package : _runtime.ResolveLegacyFramework(cached.Version) ?? package;
        CacheAvailable = cached is not null;
        CachedFileName = cached?.FileName ?? string.Empty;
        CacheStatus = cached is null
            ? $"自动扫描未找到离线安装器。请选择：{string.Join(" 或 ", LegacyFrameworkBootstrapperCatalog.GetSupportedInstallerFileNames(package))}"
            : ensured.ImportedFromBootstrapper
                ? $"已从 Bootstrapper 包目录自动导入：{cachedPackage.DisplayName}（Release ≥ {cachedPackage.ReleaseValue}）"
                : $"已缓存：{cachedPackage.DisplayName}（Release ≥ {cachedPackage.ReleaseValue}）";
        OnPropertyChanged(nameof(NeedsLegacyInstallerSelection));
    }

    /// <summary>缓存版本须与需求版本主、次版本相同，且补丁版本不低于需求版本。</summary>
    private static bool IsVersionCompatible(string cached, string required)
    {
        return Version.TryParse(cached, out var cachedVersion) &&
               Version.TryParse(required, out var requiredVersion) &&
               cachedVersion.Major == requiredVersion.Major &&
               cachedVersion.Minor == requiredVersion.Minor &&
               cachedVersion >= requiredVersion;
    }

    [RelayCommand]
    private async Task DownloadAsync(CancellationToken ct)
    {
        if (IsLegacyFramework)
        {
            _dialogs.Error("旧 .NET Framework 不通过 .NET Runtime 在线下载。请导入 ClickOnce Bootstrapper 定义对应的离线 EXE。", "不支持在线下载");
            return;
        }

        var requirement = BuildRequirement();
        if (requirement is null || string.IsNullOrEmpty(requirement.Version))
        {
            _dialogs.Error("缺少 Runtime 版本信息（请先在「应用」页完成分析）。", "无法下载");
            return;
        }

        IsDownloading = true;
        IsBusy = true;
        DownloadProgress = 0;
        DownloadProgressText = "正在检查本地已校验缓存与官方 Runtime 元数据…";
        CacheStatus = $"正在准备 {requirement.Family} {requirement.Version} {requirement.Architecture}...";

        try
        {
            var result = await _runtime.EnsureAsync(
                requirement,
                new Progress<double>(p =>
                {
                    DownloadProgress = p;
                    DownloadProgressText = $"正在下载 Runtime：{p:P0}";
                }),
                ct);

            if (!result.Success)
            {
                CacheStatus = "下载失败：" + string.Join("；", result.Errors.Select(e => $"{e.Code} {e.Message}"));
                DownloadProgressText = "下载失败。";
                _dialogs.Error(string.Join("\n", result.Errors.Select(e => $"  {e.Code} {e.Message}")), "Runtime 下载失败");
                return;
            }

            DownloadProgress = 1;
            CheckCache();

            if (result.FromCache)
            {
                CacheStatus = $"已使用本地已校验缓存：{Path.GetFileName(result.InstallerPath)}（无需下载）";
                DownloadProgressText = "无需下载，Runtime 离线安装包已就绪。";
                _dialogs.Info("已找到并使用本地已校验的 Runtime 安装包，不会重复下载。", "Runtime 已就绪");
            }
            else
            {
                CacheStatus = $"下载完成并已校验：{Path.GetFileName(result.InstallerPath)}";
                DownloadProgressText = "下载完成，Runtime 离线安装包已通过完整性校验。";
            }
        }
        catch (OperationCanceledException)
        {
            CacheStatus = "下载已取消";
            DownloadProgressText = "下载已取消。";
        }
        finally
        {
            IsDownloading = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportLocalAsync(CancellationToken ct)
    {
        if (IsLegacyFramework)
        {
            ImportLegacyFramework();
            return;
        }

        var requirement = BuildRequirement();
        if (requirement is null || string.IsNullOrEmpty(requirement.Version))
        {
            _dialogs.Error("缺少 Runtime 版本信息（请先在「应用」页完成分析）。", "无法导入");
            return;
        }

        var installerFile = _dialogs.PickFile(
            "选择已下载的 .NET Runtime 安装器",
            "Runtime 安装器 (*.exe)|*.exe|所有文件 (*.*)|*.*");
        if (string.IsNullOrEmpty(installerFile))
        {
            return;
        }

        IsBusy = true;
        DownloadProgressText = "正在读取微软官方元数据并校验本地 Runtime 安装器…";
        CacheStatus = $"正在导入 {Path.GetFileName(installerFile)}...";

        try
        {
            var result = await _runtime.ImportAsync(requirement, installerFile, ct);
            if (!result.Success)
            {
                CacheStatus = "导入失败：" + string.Join("；", result.Errors.Select(e => $"{e.Code} {e.Message}"));
                DownloadProgressText = "本地文件未导入。";
                _dialogs.Error(string.Join("\n", result.Errors.Select(e => $"  {e.Code} {e.Message}")), "Runtime 导入失败");
                return;
            }

            CheckCache();
            CacheStatus = $"已校验并导入本地 Runtime：{Path.GetFileName(result.InstallerPath)}";
            DownloadProgressText = "本地安装器已通过完整性校验并写入缓存。";
            _dialogs.Info("本地 Runtime 安装器已通过微软官方摘要校验，并已写入离线缓存。", "Runtime 已导入");
        }
        catch (OperationCanceledException)
        {
            CacheStatus = "导入已取消";
            DownloadProgressText = "导入已取消。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ImportLegacyFramework()
    {
        var version = _project?.RuntimeVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            _dialogs.Error("缺少 .NET Framework 版本信息（请先在“应用”页完成分析）。", "无法导入");
            return;
        }

        var package = _runtime.ResolveLegacyFramework(version);
        if (package is null)
        {
            _dialogs.Error(version.StartsWith("3.5", StringComparison.Ordinal)
                ? ".NET Framework 3.5 SP1 需要完整多文件 Bootstrapper 载荷，当前不支持单 EXE 离线嵌入。"
                : $"未找到 .NET Framework {version} 的 ClickOnce Bootstrapper 离线包定义。", "无法导入");
            return;
        }

        var installerFile = _dialogs.PickFile(
            $"选择 .NET Framework {version} 或更高版本的离线安装器",
            "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            Directory.Exists(package.PackageDirectory)
                ? package.PackageDirectory
                : LegacyFrameworkBootstrapperCatalog.DefaultPackagesDirectory);
        if (string.IsNullOrEmpty(installerFile)) return;

        var result = _runtime.ImportLegacyFramework(version, installerFile);
        if (result.Error is not null)
        {
            CacheStatus = "导入失败：" + result.Error.Message;
            _dialogs.Error(result.Error.Message, "导入失败");
            return;
        }

        CheckLegacyFrameworkCache();
        _dialogs.Info(".NET Framework 离线安装器已复制到 DotSetupForge 缓存，原文件未修改。", "导入完成");
    }
}

/// <summary>部署模式选项。</summary>
public sealed record DeploymentModeOption(
    string Value,
    string Title,
    string Description,
    bool Recommended);
