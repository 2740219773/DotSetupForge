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
    private bool _isBusy;

    public void Bind(EditableProject project)
    {
        _project = project;
        OnPropertyChanged(nameof(Project));
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
        var requirement = BuildRequirement();
        if (requirement is null || string.IsNullOrEmpty(requirement.Version))
        {
            CacheStatus = "缺少 Runtime 版本信息（请先在「应用」页完成分析）";
            CacheAvailable = false;
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
    }

    /// <summary>major.minor 兼容：缓存版本 >= 需求版本且同一主版本。</summary>
    private static bool IsVersionCompatible(string cached, string required)
    {
        var cachedMajor = MajorMinor(cached);
        var requiredMajor = MajorMinor(required);
        return cachedMajor.Major == requiredMajor.Major &&
               cachedMajor.Minor >= requiredMajor.Minor;
    }

    private static (int Major, int Minor) MajorMinor(string version)
    {
        var parts = version.Split('.');
        _ = int.TryParse(parts.Length > 0 ? parts[0] : "0", out var major);
        _ = int.TryParse(parts.Length > 1 ? parts[1] : "0", out var minor);
        return (major, minor);
    }

    [RelayCommand]
    private async Task DownloadAsync(CancellationToken ct)
    {
        var requirement = BuildRequirement();
        if (requirement is null || string.IsNullOrEmpty(requirement.Version))
        {
            _dialogs.Error("缺少 Runtime 版本信息（请先在「应用」页完成分析）。", "无法下载");
            return;
        }

        IsDownloading = true;
        IsBusy = true;
        DownloadProgress = 0;
        CacheStatus = $"正在解析 {requirement.Family} {requirement.Version} {requirement.Architecture}...";

        try
        {
            var result = await _runtime.EnsureAsync(
                requirement,
                new Progress<double>(p => DownloadProgress = p),
                ct);

            if (!result.Success)
            {
                CacheStatus = "下载失败：" + string.Join("；", result.Errors.Select(e => $"{e.Code} {e.Message}"));
                _dialogs.Error(string.Join("\n", result.Errors.Select(e => $"  {e.Code} {e.Message}")), "Runtime 下载失败");
                return;
            }

            CacheStatus = result.FromCache
                ? $"命中缓存：{Path.GetFileName(result.InstallerPath)}"
                : $"下载完成：{Path.GetFileName(result.InstallerPath)}";
            DownloadProgress = 1;
            CheckCache();
        }
        catch (OperationCanceledException)
        {
            CacheStatus = "下载已取消";
        }
        finally
        {
            IsDownloading = false;
            IsBusy = false;
        }
    }
}

/// <summary>部署模式选项。</summary>
public sealed record DeploymentModeOption(
    string Value,
    string Title,
    string Description,
    bool Recommended);
