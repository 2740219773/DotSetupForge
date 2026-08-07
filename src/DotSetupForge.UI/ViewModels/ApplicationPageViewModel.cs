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

    public void Bind(EditableProject project)
    {
        _project = project;
        OnPropertyChanged(nameof(Project));
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

        var result = _analysis.Analyze(_project.SourcePath);
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
    }
}
