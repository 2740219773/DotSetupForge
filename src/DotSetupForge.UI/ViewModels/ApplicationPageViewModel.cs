using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotSetupForge.Application.Analysis;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.Services;

namespace DotSetupForge.UI.ViewModels;

/// <summary>应用页：源目录 + 产品信息 + 自动分析结果。</summary>
public partial class ApplicationPageViewModel : ObservableObject, IProjectPageViewModel
{
    private readonly IDialogService _dialogs;
    private readonly ApplicationAnalysisService _analysis;
    private EditableProject? _project;

    public ApplicationPageViewModel(IDialogService dialogs, ApplicationAnalysisService analysis)
    {
        _dialogs = dialogs;
        _analysis = analysis;
    }

    public EditableProject? Project => _project;

    // ---- 分析结果展示 ----

    [ObservableProperty]
    private bool _analysisSucceeded;

    [ObservableProperty]
    private string _analysisStatus = "尚未分析";

    [ObservableProperty]
    private string _applicationType = "—";

    [ObservableProperty]
    private string _targetFramework = "—";

    [ObservableProperty]
    private string _frameworkName = "—";

    [ObservableProperty]
    private string _frameworkVersion = "—";

    [ObservableProperty]
    private string _runtimeName = "—";

    [ObservableProperty]
    private string _architecture = "—";

    [ObservableProperty]
    private string _deploymentMode = "—";

    [ObservableProperty]
    private string _fileCount = "—";

    [ObservableProperty]
    private string _setupIconStatus = "将在分析发布目录时自动检测 ICO 图标。";

    public void Bind(EditableProject project)
    {
        _project = project;
        OnPropertyChanged(nameof(Project));
        DetectSetupIcon(project.SourcePath);
    }

    [RelayCommand]
    private void ChangeSourceDirectory()
    {
        if (_project is null)
        {
            return;
        }

        var directory = _dialogs.PickFolder("选择应用发布目录（推荐 publish 目录）");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _project.SourcePath = directory;
        Reanalyze();
    }

    [RelayCommand]
    private void SelectSetupIcon()
    {
        if (_project is null)
        {
            return;
        }

        var iconPath = _dialogs.PickFile("选择安装程序 ICO 图标", "ICO 图标 (*.ico)|*.ico");
        if (!string.IsNullOrEmpty(iconPath))
        {
            _project.SetupIconPath = iconPath;
            SetupIconStatus = "已手动选择 ICO 图标；将同时用于安装程序和快捷方式。";
        }
    }

    [RelayCommand]
    private void ClearSetupIcon()
    {
        if (_project is not null)
        {
            _project.SetupIconPath = string.Empty;
            SetupIconStatus = "已清除图标；构建时将使用默认安装程序和快捷方式图标。";
        }
    }

    [RelayCommand]
    private void Reanalyze()
    {
        if (_project is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_project.SourcePath) ||
            !Directory.Exists(_project.SourcePath))
        {
            AnalysisStatus = "源目录不存在";
            AnalysisSucceeded = false;
            return;
        }

        DetectSetupIcon(_project.SourcePath);

        var mainExecutable = string.IsNullOrWhiteSpace(_project.MainExecutable)
            ? null
            : _project.MainExecutable;
        var result = _analysis.Analyze(_project.SourcePath, mainExecutable);
        if (!result.Success)
        {
            AnalysisStatus = "分析失败：" + string.Join("；", result.Diagnostics.Select(d => $"{d.Code} {d.Message}"));
            AnalysisSucceeded = false;
            return;
        }

        // 展示
        ApplicationType = result.ApplicationType.ToString();
        TargetFramework = result.TargetFramework;
        FrameworkName = result.FrameworkName;
        FrameworkVersion = result.FrameworkVersion;
        RuntimeName = result.RuntimeName;
        Architecture = result.Architecture.ToString();
        DeploymentMode = result.DeploymentMode.ToString();
        FileCount = $"{result.Files.Count} 个文件";
        AnalysisStatus = "分析正常";
        AnalysisSucceeded = true;

        // 自动填充空字段（不覆盖用户已填内容）
        if (string.IsNullOrWhiteSpace(_project.ProductName))
        {
            _project.ProductName = result.ApplicationName;
        }

        if (string.IsNullOrWhiteSpace(_project.MainExecutable))
        {
            _project.MainExecutable = Path.GetFileName(result.MainExecutable ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(_project.Version) || _project.Version == "1.0.0")
        {
            _project.Version = string.IsNullOrEmpty(result.Version) ? "1.0.0" : result.Version;
        }

        if (string.IsNullOrWhiteSpace(_project.RuntimeVersion))
        {
            var parts = result.FrameworkVersion.Split('.');
            _project.RuntimeVersion = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : result.FrameworkVersion;
        }

        if (_project.AutoDetect)
        {
            _project.RuntimeFamily = result.FrameworkName == "Microsoft.NETFramework"
                ? nameof(RuntimeFamily.NetFramework)
                : result.FrameworkName == "Microsoft.WindowsDesktop.App"
                    ? nameof(RuntimeFamily.WindowsDesktop)
                    : nameof(RuntimeFamily.DotNet);
            _project.RuntimeArchitecture = result.Architecture.ToString();
            if (result.DeploymentMode == DotSetupForge.Core.Analysis.DeploymentMode.LegacyFramework)
            {
                _project.RuntimeMode = nameof(RuntimeDeploymentMode.SmartOffline);
            }
        }
    }

    private void DetectSetupIcon(string sourceDirectory)
    {
        if (_project is null || string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            SetupIconStatus = "请选择有效发布目录后自动检测 ICO 图标。";
            return;
        }

        try
        {
            var icons = Directory.EnumerateFiles(sourceDirectory, "*.ico", SearchOption.AllDirectories)
                .Take(2)
                .ToList();

            switch (icons.Count)
            {
                case 0:
                    SetupIconStatus = "发布目录中未找到 ICO 图标，可手动选择。";
                    break;
                case 1 when string.IsNullOrWhiteSpace(_project.SetupIconPath):
                    _project.SetupIconPath = icons[0];
                    SetupIconStatus = "已自动选择发布目录中唯一的 ICO 图标。";
                    break;
                case 1:
                    SetupIconStatus = "发布目录中找到唯一 ICO，已保留当前手动选择的图标。";
                    break;
                default:
                    SetupIconStatus = "发布目录中找到多个 ICO 图标，请手动选择。";
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetupIconStatus = "无法扫描发布目录中的 ICO 图标，请手动选择。";
        }
    }
}
