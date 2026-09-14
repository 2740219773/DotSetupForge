using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotSetupForge.Application.Analysis;
using DotSetupForge.Application.Projects;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Models;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.Services;

namespace DotSetupForge.UI.ViewModels;

/// <summary>主窗口 ViewModel：首页（最近项目）+ 步骤导航 + 项目级命令。</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly RecentProjectsService _recentProjects;
    private readonly ManagedProjectStore _managedProjects;
    private readonly PackageProjectService _projects = new();
    private readonly ApplicationAnalysisService _analysis = new();

    // ---- 首页 ----

    public ObservableCollection<RecentProjectEntry> RecentProjects { get; } = [];

    [ObservableProperty]
    private string? _recentError;

    // ---- 当前项目状态 ----

    public EditableProject? Project { get; private set; }

    [ObservableProperty]
    private bool _isProjectOpen;

    [ObservableProperty]
    private string _projectDisplayName = "未命名项目";

    [ObservableProperty]
    private string? _projectPath;

    [ObservableProperty]
    private string _pageTitle = "首页";

    [ObservableProperty]
    private PageKind _currentPageKind = PageKind.Home;

    // ---- 页面 ----

    public ApplicationPageViewModel ApplicationPage { get; }
    public FilesPageViewModel FilesPage { get; }
    public RuntimePageViewModel RuntimePage { get; }
    public InstallerPageViewModel InstallerPage { get; }
    public VersionSigningPageViewModel VersionSigningPage { get; }
    public BuildPageViewModel BuildPage { get; }

    [ObservableProperty]
    private object _currentPage;

    public MainViewModel(IDialogService dialogs)
    {
        Dialogs = dialogs;
        _recentProjects = new RecentProjectsService();
        _managedProjects = new ManagedProjectStore();

        ApplicationPage = new ApplicationPageViewModel(dialogs, _analysis);
        FilesPage = new FilesPageViewModel(dialogs, _analysis);
        RuntimePage = new RuntimePageViewModel(dialogs);
        InstallerPage = new InstallerPageViewModel(dialogs);
        VersionSigningPage = new VersionSigningPageViewModel(dialogs);
        BuildPage = new BuildPageViewModel(dialogs, _projects);

        _currentPage = this; // 初始显示首页（Home 复用 MainViewModel）
        RefreshRecentProjects();
    }

    public IDialogService Dialogs { get; }

    // ================= 首页 =================

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var entry in _recentProjects.List())
        {
            RecentProjects.Add(entry);
        }
    }

    [RelayCommand]
    private void NewProjectFromDirectory()
    {
        var directory = Dialogs.PickFolder("选择应用发布目录（推荐 publish 目录）");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var result = _analysis.Analyze(directory);
        if (!result.Success && result.MainExecutableCandidates.Count > 0)
        {
            var selected = Dialogs.PickFile(
                "选择要创建快捷方式并在安装后启动的主程序",
                "可选主程序 (*.exe)|*.exe",
                directory);
            if (string.IsNullOrEmpty(selected))
            {
                return;
            }

            var selectedRelativePath = Path.GetRelativePath(directory, selected).Replace('\\', '/');
            if (!result.MainExecutableCandidates.Contains(selectedRelativePath, StringComparer.OrdinalIgnoreCase))
            {
                Dialogs.Error("请选择当前发布目录中列出的候选主程序。", "主程序无效");
                return;
            }

            result = _analysis.Analyze(directory, selectedRelativePath);
        }

        if (!result.Success)
        {
            Dialogs.Error(
                "无法分析该目录：\n" + string.Join("\n", result.Diagnostics.Select(d => $"  {d.Code} {d.Message}")),
                "分析失败");
            return;
        }

        var project = CreateProjectFromAnalysis(directory, result);
        OpenProject(project, null);
        SaveManagedProject();

        Dialogs.Info(
            $"已自动识别：\n" +
            $"  主程序：{Path.GetFileName(result.MainExecutable)}\n" +
            $"  框架：{result.FrameworkName} {result.FrameworkVersion}\n" +
            $"  架构：{result.Architecture}\n" +
            $"  部署：{result.DeploymentMode}",
            "分析完成");
    }

    [RelayCommand]
    private void NewProjectFromCsproj()
    {
        Dialogs.Info(
            "「从 C# 项目创建」将在后续版本支持（需要自动执行 dotnet publish）。\n" +
            "当前请先编译并发布程序后，使用「从程序目录创建」。",
            "即将支持");
    }

    [RelayCommand]
    private void OpenProjectFile()
    {
        string initialDirectory;
        try
        {
            initialDirectory = _managedProjects.GetProjectDirectory();
        }
        catch (IOException ex)
        {
            Dialogs.Error($"无法打开项目配置目录：{ex.Message}", "打开失败");
            return;
        }

        var path = Dialogs.PickFile(
            "打开打包项目",
            "打包项目 (*.pack.json)|*.pack.json|所有文件 (*.*)|*.*",
            initialDirectory);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        LoadProject(path);
    }

    [RelayCommand]
    private void OpenRecentProject(RecentProjectEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        if (!File.Exists(entry.Path))
        {
            _recentProjects.Remove(entry.Path);
            RefreshRecentProjects();
            return;
        }

        LoadProject(entry.Path);
    }

    [RelayCommand]
    private void RemoveRecentProject(RecentProjectEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _recentProjects.Remove(entry.Path);
        RefreshRecentProjects();
    }

    private static EditableProject CreateProjectFromAnalysis(string directory, ApplicationAnalysisResult result)
    {
        var project = new EditableProject
        {
            ProductName = result.ApplicationName,
            Version = string.IsNullOrEmpty(result.Version) ? "1.0.0" : result.Version,
            MainExecutable = Path.GetFileName(result.MainExecutable ?? string.Empty),
            Publisher = string.Empty,
            SourcePath = directory,
            SourceType = "目录",
            AutoDetect = true,
            RuntimeFamily = result.FrameworkName switch
            {
                "Microsoft.NETFramework" => nameof(RuntimeFamily.NetFramework),
                "Microsoft.WindowsDesktop.App" => nameof(RuntimeFamily.WindowsDesktop),
                "Microsoft.AspNetCore.App" => nameof(RuntimeFamily.AspNetCore),
                _ => nameof(RuntimeFamily.DotNet),
            },
            RuntimeVersion = NormalizeRuntimeVersion(result.FrameworkVersion),
            RuntimeArchitecture = result.Architecture.ToString(),
            RuntimeMode = result.DeploymentMode switch
            {
                DeploymentMode.LegacyFramework => nameof(RuntimeDeploymentMode.SmartOffline),
                DeploymentMode.SelfContained => nameof(RuntimeDeploymentMode.SelfContained),
                _ => nameof(RuntimeDeploymentMode.SmartOffline),
            },
        };
        return project;
    }

    /// <summary>10.0.0 → 10.0；保留前两段。</summary>
    private static string NormalizeRuntimeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var parts = version.Split('.');
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : version;
    }

    // ================= 项目 =================

    private void LoadProject(string path)
    {
        if (!File.Exists(path))
        {
            Dialogs.Error($"配置文件不存在：{path}", "打开失败");
            return;
        }

        var load = _projects.Deserialize(File.ReadAllText(path));
        if (!load.Success || load.Project is null)
        {
            Dialogs.Error(
                "无法解析配置文件：\n" + string.Join("\n", load.Errors.Select(e => $"  {e.Code} {e.Message}")),
                "打开失败");
            return;
        }

        OpenProject(EditableProject.FromProject(load.Project), path);
    }

    /// <summary>打开项目：绑定所有页面，进入步骤导航。</summary>
    private void OpenProject(EditableProject project, string? path)
    {
        Project = project;
        ProjectPath = path;
        ProjectDisplayName = string.IsNullOrEmpty(project.ProductName) ? "未命名项目" : project.ProductName;
        IsProjectOpen = true;

        ApplicationPage.Bind(project);
        FilesPage.Bind(project);
        RuntimePage.Bind(project);
        InstallerPage.Bind(project);
        VersionSigningPage.Bind(project);
        BuildPage.Bind(project);

        if (!string.IsNullOrEmpty(path))
        {
            _recentProjects.Add(path, ProjectDisplayName);
            RefreshRecentProjects();
        }

        NavigateTo(PageKind.Application);
    }

    [RelayCommand]
    private void SaveProject()
    {
        if (Project is null)
        {
            return;
        }

        var json = _projects.Serialize(Project.ToProject());

        if (string.IsNullOrEmpty(ProjectPath))
        {
            SaveManagedProject();
            return;
        }

        try
        {
            File.WriteAllText(ProjectPath!, json);
            _recentProjects.Add(ProjectPath!, ProjectDisplayName);
            RefreshRecentProjects();
            Dialogs.Info($"项目已保存：{ProjectPath}", "保存成功");
        }
        catch (IOException ex)
        {
            Dialogs.Error($"保存失败：{ex.Message}", "保存失败");
        }
    }

    [RelayCommand]
    private void SaveProjectAs()
    {
        if (Project is null)
        {
            return;
        }

        var path = SavePackDialog();
        if (path is null)
        {
            return;
        }

        ProjectPath = path;
        SaveProject();
    }

    /// <summary>不询问用户，将新建项目保存到打包工具管理的配置目录。</summary>
    private void SaveManagedProject()
    {
        if (Project is null)
        {
            return;
        }

        try
        {
            var json = _projects.Serialize(Project.ToProject());
            ProjectPath = _managedProjects.Save(ProjectDisplayName, Project.SourcePath, json);
            _recentProjects.Add(ProjectPath, ProjectDisplayName);
            RefreshRecentProjects();
        }
        catch (IOException ex)
        {
            Dialogs.Error($"无法自动保存打包项目配置：{ex.Message}", "保存失败");
        }
    }

    private string? SavePackDialog()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存打包项目",
            Filter = "打包项目 (*.pack.json)|*.pack.json",
            FileName = $"{ProjectDisplayName}.pack.json",
            DefaultExt = ".pack.json",
            AddExtension = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    // ================= 导航 =================

    [RelayCommand]
    private void NavigateTo(PageKind page)
    {
        if (!IsProjectOpen && page != PageKind.Home)
        {
            return;
        }

        CurrentPage = page switch
        {
            PageKind.Home => this,
            PageKind.Application => ApplicationPage,
            PageKind.Files => FilesPage,
            PageKind.Runtime => RuntimePage,
            PageKind.Installer => InstallerPage,
            PageKind.VersionSigning => VersionSigningPage,
            PageKind.Build => BuildPage,
            _ => this,
        };

        PageTitle = page switch
        {
            PageKind.Home => "首页",
            PageKind.Application => "应用",
            PageKind.Files => "文件",
            PageKind.Runtime => "运行环境",
            PageKind.Installer => "安装设置",
            PageKind.VersionSigning => "版本与签名",
            PageKind.Build => "构建",
            _ => "首页",
        };

        CurrentPageKind = page;
    }
}

/// <summary>左侧导航页。</summary>
public enum PageKind
{
    Home,
    Application,
    Files,
    Runtime,
    Installer,
    VersionSigning,
    Build,
}
