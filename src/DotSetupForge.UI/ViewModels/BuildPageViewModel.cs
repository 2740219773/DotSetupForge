using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotSetupForge.Application.Build;
using DotSetupForge.Application.Projects;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.Services;

namespace DotSetupForge.UI.ViewModels;

/// <summary>构建页：Pipeline 步骤状态 + 实时日志 + 产物。</summary>
public partial class BuildPageViewModel : ObservableObject, IProjectPageViewModel
{
    private readonly IDialogService _dialogs;
    private readonly PackageProjectService _projects;
    private readonly Dispatcher _dispatcher;
    private EditableProject? _project;
    private CancellationTokenSource? _cts;

    public BuildPageViewModel(IDialogService dialogs, PackageProjectService projects)
    {
        _dialogs = dialogs;
        _projects = projects;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    // ---- Pipeline 步骤 ----

    public ObservableCollection<BuildStepViewModel> Steps { get; } = [];

    // ---- 日志 ----

    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    private bool _isBuilding;

    [ObservableProperty]
    private bool _buildSucceeded;

    [ObservableProperty]
    private string _artifactPath = string.Empty;

    [ObservableProperty]
    private string _artifactSize = string.Empty;

    [ObservableProperty]
    private string _buildSummary = string.Empty;

    [ObservableProperty]
    private string _statusText = "准备就绪";

    public void Bind(EditableProject project)
    {
        _project = project;
    }

    private void ResetPipeline()
    {
        Steps.Clear();
        Steps.Add(new BuildStepViewModel("验证项目"));
        Steps.Add(new BuildStepViewModel("应用分析"));
        Steps.Add(new BuildStepViewModel("文件收集"));
        Steps.Add(new BuildStepViewModel("Runtime 准备"));
        Steps.Add(new BuildStepViewModel("生成安装脚本"));
        Steps.Add(new BuildStepViewModel("编译安装包"));
        Steps.Add(new BuildStepViewModel("数字签名"));
        Steps.Add(new BuildStepViewModel("验证安装包"));

        LogLines.Clear();
        ArtifactPath = string.Empty;
        ArtifactSize = string.Empty;
        BuildSucceeded = false;
        BuildSummary = string.Empty;
        StatusText = "准备就绪";
    }

    private void AppendLog(string message)
    {
        _dispatcher.Invoke(() => LogLines.Add($"[{DateTime.Now:HH:mm:ss}] {message}"));
    }

    private void AdvanceTo(string title, string detail)
    {
        _dispatcher.Invoke(() =>
        {
            var found = false;
            foreach (var step in Steps)
            {
                if (step.Title == title)
                {
                    step.Status = BuildStepStatus.Running;
                    step.Detail = detail;
                    found = true;
                }
                else if (found)
                {
                    step.Status = BuildStepStatus.Pending;
                }
                else if (step.Status == BuildStepStatus.Running)
                {
                    step.Status = BuildStepStatus.Completed;
                }
            }
        });
    }

    private void CompleteAll(string detail = "")
    {
        _dispatcher.Invoke(() =>
        {
            foreach (var step in Steps)
            {
                if (step.Status != BuildStepStatus.Failed)
                {
                    step.Status = BuildStepStatus.Completed;
                    if (!string.IsNullOrEmpty(detail))
                    {
                        step.Detail = detail;
                    }
                }
            }
        });
    }

    private void FailStep(string title, string detail)
    {
        _dispatcher.Invoke(() =>
        {
            foreach (var step in Steps)
            {
                if (step.Title == title)
                {
                    step.Status = BuildStepStatus.Failed;
                    step.Detail = detail;
                }
                else if (step.Status == BuildStepStatus.Running)
                {
                    step.Status = BuildStepStatus.Completed;
                }
            }
        });
    }

    private void SkipStep(string title, string detail)
    {
        _dispatcher.Invoke(() =>
        {
            foreach (var step in Steps)
            {
                if (step.Title == title)
                {
                    step.Status = BuildStepStatus.Skipped;
                    step.Detail = detail;
                }
            }
        });
    }

    // ================= 构建 =================

    [RelayCommand]
    private async Task BuildAsync(CancellationToken ct)
    {
        if (_project is null)
        {
            return;
        }

        // 校验输入
        var errors = Validate();
        if (errors.Count > 0)
        {
            _dialogs.Error(string.Join("\n", errors), "项目验证失败");
            return;
        }

        ResetPipeline();
        IsBuilding = true;
        BuildSucceeded = false;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            var started = DateTime.UtcNow;

            // 1. 验证项目
            AdvanceTo("验证项目", "配置完整");
            AppendLog("验证项目配置...");

            // 2-6. 构建（由 progress 消息推进步骤）
            var progress = new Progress<string>(message =>
            {
                AppendLog(message);
                AdvanceStepFromMessage(message);
            });

            var request = new BuildRequest(
                _project.SourcePath,
                _project.ToProject(),
                null);

            var result = await new BuildService().BuildAsync(request, progress, _cts.Token);

            if (result.Success)
            {
                CompleteAll();
                BuildSucceeded = true;
                ArtifactPath = result.Artifacts.FirstOrDefault() ?? string.Empty;
                if (File.Exists(ArtifactPath))
                {
                    ArtifactSize = FileNode.FormatSize(new FileInfo(ArtifactPath).Length);
                }

                BuildSummary = $"构建成功，耗时 {result.Duration.TotalSeconds:F1} 秒";
                StatusText = "构建成功";
                AppendLog($"构建成功，耗时 {result.Duration.TotalSeconds:F1} 秒");
                AppendLog($"产物：{ArtifactPath}");
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    AppendLog($"错误 {error.Code}: {error.Message}");
                }

                FailStep(CurrentRunningStepTitle(), string.Join("；", result.Errors.Select(e => $"{e.Code} {e.Message}")));
                StatusText = "构建失败";
                BuildSummary = $"构建失败（{result.Duration.TotalSeconds:F1} 秒）";
                _dialogs.Error(string.Join("\n", result.Errors.Select(e => $"  {e.Code} {e.Message}")), "构建失败");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "构建已取消";
            AppendLog("构建已取消");
        }
        catch (Exception ex)
        {
            StatusText = "构建异常";
            AppendLog($"未处理异常：{ex.Message}");
            FailStep(CurrentRunningStepTitle(), ex.Message);
            _dialogs.Error(ex.Message, "构建异常");
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private string CurrentRunningStepTitle()
    {
        var running = Steps.FirstOrDefault(s => s.Status == BuildStepStatus.Running);
        return running?.Title ?? "编译安装包";
    }

    private void AdvanceStepFromMessage(string message)
    {
        if (message.Contains("分析应用", StringComparison.Ordinal))
        {
            AdvanceTo("应用分析", "识别主程序与框架");
        }
        else if (message.Contains("构建安装模型", StringComparison.Ordinal))
        {
            AdvanceTo("文件收集", "按规则筛选文件");
        }
        else if (message.Contains("准备 .NET Runtime", StringComparison.Ordinal)
                 || message.Contains("Runtime", StringComparison.Ordinal))
        {
            AdvanceTo("Runtime 准备", message);
        }
        else if (message.Contains("installer.iss", StringComparison.Ordinal))
        {
            AdvanceTo("生成安装脚本", "Scriban 渲染 installer.iss");
        }
        else if (message.Contains("编译安装包", StringComparison.Ordinal))
        {
            AdvanceTo("编译安装包", "ISCC 编译中");
        }
        else if (message.Contains("完成", StringComparison.Ordinal))
        {
            CompleteAll();
        }
    }

    private List<string> Validate()
    {
        var errors = new List<string>();
        if (_project is null)
        {
            return errors;
        }

        if (string.IsNullOrWhiteSpace(_project.ProductName))
        {
            errors.Add("· 产品名称不能为空（请在「应用」页填写）");
        }

        if (string.IsNullOrWhiteSpace(_project.SourcePath) || !Directory.Exists(_project.SourcePath))
        {
            errors.Add("· 源目录不存在（请在「应用」页选择发布目录）");
        }

        if (string.IsNullOrWhiteSpace(_project.MainExecutable))
        {
            errors.Add("· 主程序未识别（请确认分析成功）");
        }

        return errors;
    }

    [RelayCommand]
    private void CancelBuild()
    {
        _cts?.Cancel();
        AppendLog("正在取消...");
    }

    [RelayCommand]
    private void OpenArtifactFolder()
    {
        if (string.IsNullOrEmpty(ArtifactPath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(ArtifactPath);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"/select,\"{ArtifactPath}\"",
                UseShellExecute = true,
            });
        }
    }
}

/// <summary>构建步骤状态。</summary>
public enum BuildStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
}

/// <summary>构建步骤（标题 + 状态 + 详情）。</summary>
public partial class BuildStepViewModel : ObservableObject
{
    public BuildStepViewModel(string title)
    {
        Title = title;
    }

    public string Title { get; }

    [ObservableProperty]
    private BuildStepStatus _status = BuildStepStatus.Pending;

    [ObservableProperty]
    private string _detail = string.Empty;

    partial void OnStatusChanged(BuildStepStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusIcon));
    }

    public string StatusText => Status switch
    {
        BuildStepStatus.Running => "进行中",
        BuildStepStatus.Completed => "完成",
        BuildStepStatus.Failed => "失败",
        BuildStepStatus.Skipped => "跳过",
        _ => "等待",
    };

    public string StatusIcon => Status switch
    {
        BuildStepStatus.Completed => "✓",
        BuildStepStatus.Running => "→",
        BuildStepStatus.Failed => "✕",
        BuildStepStatus.Skipped => "–",
        _ => "○",
    };
}
