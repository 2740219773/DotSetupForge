using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotSetupForge.Application.Analysis;
using DotSetupForge.Core.Analysis;
using DotSetupForge.Core.Packaging;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.Services;

namespace DotSetupForge.UI.ViewModels;

/// <summary>文件页：文件树（勾选控制包含/排除）+ 右侧属性面板。</summary>
public partial class FilesPageViewModel : ObservableObject, IProjectPageViewModel
{
    private readonly IDialogService _dialogs;
    private readonly ApplicationAnalysisService _analysis;
    private readonly Dispatcher _dispatcher;
    private EditableProject? _project;
    private bool _applyingRules;

    public FilesPageViewModel(IDialogService dialogs, ApplicationAnalysisService analysis)
    {
        _dialogs = dialogs;
        _analysis = analysis;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public ObservableCollection<FileNode> RootNodes { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "尚未分析";

    [ObservableProperty]
    private int _includedCount;

    [ObservableProperty]
    private int _excludedCount;

    [ObservableProperty]
    private string _totalSizeText = string.Empty;

    // ---- 右侧属性 ----

    [ObservableProperty]
    private FileNode? _selectedNode;

    [ObservableProperty]
    private string _selectedName = "—";

    [ObservableProperty]
    private string _selectedPath = "—";

    [ObservableProperty]
    private string _selectedSize = "—";

    [ObservableProperty]
    private string _selectedCategory = "—";

    [ObservableProperty]
    private string _selectedLocation = "—";

    [ObservableProperty]
    private string _selectedUpgrade = "—";

    [ObservableProperty]
    private string _selectedUninstall = "—";

    [ObservableProperty]
    private bool _hasSelection;

    public void Bind(EditableProject project)
    {
        _project = project;
        RefreshCommand.Execute(null);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_project is null)
        {
            return;
        }

        var directory = _project.SourcePath;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            StatusText = "源目录不存在";
            return;
        }

        IsLoading = true;
        StatusText = "正在分析文件...";
        var analysis = await Task.Run(() => _analysis.Analyze(directory));

        if (!analysis.Success)
        {
            IsLoading = false;
            StatusText = "分析失败：" + string.Join("；", analysis.Diagnostics.Select(d => $"{d.Code} {d.Message}"));
            return;
        }

        // 构建树（后台线程做规则判定）
        var (rootNodes, included, excluded, totalSize) = await Task.Run(() =>
        {
            var engine = new FileRuleEngine();
            var userRules = BuildUserRules();
            var result = engine.Apply(analysis.Files, userRules);

            var excludedSet = result.Excluded
                .Select(f => f.RelativePath.Replace('\\', '/').ToLowerInvariant())
                .ToHashSet();

            var nodes = BuildTree(analysis.Files, excludedSet);
            return (nodes, result.Included.Count, result.Excluded.Count,
                result.Included.Sum(f => f.Size));
        });

        // 回到 UI 线程更新
        _dispatcher.Invoke(() =>
        {
            RootNodes.Clear();
            foreach (var node in rootNodes)
            {
                RootNodes.Add(node);
            }

            IncludedCount = included;
            ExcludedCount = excluded;
            TotalSizeText = FileNode.FormatSize(totalSize);
            StatusText = $"共 {analysis.Files.Count} 个文件，包含 {included} / 排除 {excluded}";
            IsLoading = false;
        });
    }

    private List<FileRule> BuildUserRules()
    {
        var rules = new List<FileRule>();
        if (_project is null)
        {
            return rules;
        }

        rules.AddRange(_project.IncludePatterns.Select(p => new FileRule(p, FileRuleAction.Include)));
        rules.AddRange(_project.ExcludePatterns.Select(p => new FileRule(p, FileRuleAction.Exclude)));
        return rules;
    }

    private List<FileNode> BuildTree(IReadOnlyList<ScannedFile> files, HashSet<string> excludedSet)
    {
        var roots = new List<FileNode>();
        var dirs = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);

        FileNode GetOrCreateDirectory(string relDir)
        {
            if (string.IsNullOrEmpty(relDir))
            {
                // 根目录 → null，文件挂到 roots
                return null!;
            }

            if (dirs.TryGetValue(relDir, out var existing))
            {
                return existing;
            }

            var parentRel = Path.GetDirectoryName(relDir)?.Replace('\\', '/') ?? string.Empty;
            var parent = string.IsNullOrEmpty(parentRel) ? null : GetOrCreateDirectory(parentRel);
            var node = new FileNode(Path.GetFileName(relDir), relDir, isDirectory: true, parent);
            dirs[relDir] = node;

            if (parent is null)
            {
                roots.Add(node);
            }
            else
            {
                parent.Children.Add(node);
            }

            return node;
        }

        foreach (var file in files.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var rel = file.RelativePath.Replace('\\', '/');
            var relDir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty;

            FileNode node;
            if (string.IsNullOrEmpty(relDir))
            {
                node = new FileNode(file.FileName, rel, isDirectory: false, null);
                roots.Add(node);
            }
            else
            {
                var dir = GetOrCreateDirectory(relDir);
                node = new FileNode(file.FileName, rel, isDirectory: false, dir);
                dir.Children.Add(node);
            }

            node.Scanned = file;
            node.RuleExcluded = excludedSet.Contains(rel.ToLowerInvariant());
            node.SetState(!node.RuleExcluded, cascade: false);
        }

        // 目录节点按深度降序聚合勾选状态
        foreach (var dir in dirs.Values
                     .OrderByDescending(d => d.RelativePath.Count(c => c == '/')))
        {
            dir.RefreshState();
        }

        return roots;
    }

    // ================= 勾选交互 =================

    /// <summary>文件节点勾选变化：把精确相对路径写入/移出排除规则。</summary>
    [RelayCommand]
    private void ToggleInclude(FileNode? node)
    {
        if (_project is null || node is null || _applyingRules)
        {
            return;
        }

        node.RefreshState();

        foreach (var fileNode in node.EnumerateFiles())
        {
            var pattern = fileNode.RelativePath.Replace('\\', '/');
            if (fileNode.CheckedState == true)
            {
                _project.ExcludePatterns.Remove(pattern);
            }
            else if (!_project.ExcludePatterns.Contains(pattern))
            {
                _project.ExcludePatterns.Add(pattern);
            }
        }

        // 目录级模式同步（供高级用户理解）
        if (node.IsDirectory)
        {
            var dirPattern = $"{node.RelativePath.Replace('\\', '/')}/**";
            if (node.CheckedState == true)
            {
                _project.ExcludePatterns.Remove(dirPattern);
            }
            else if (!_project.ExcludePatterns.Contains(dirPattern))
            {
                _project.ExcludePatterns.Add(dirPattern);
            }
        }

        UpdateCounts();
    }

    private void UpdateCounts()
    {
        if (_project is null)
        {
            return;
        }

        _applyingRules = true;
        try
        {
            var engine = new FileRuleEngine();
            var result = engine.Apply(
                RootNodes.SelectMany(n => n.EnumerateFiles())
                    .Where(n => n.Scanned is not null)
                    .Select(n => n.Scanned!)
                    .ToList(),
                BuildUserRules());

            IncludedCount = result.Included.Count;
            ExcludedCount = result.Excluded.Count;
            TotalSizeText = FileNode.FormatSize(result.Included.Sum(f => f.Size));
        }
        finally
        {
            _applyingRules = false;
        }
    }

    // ================= 选中属性 =================

    partial void OnSelectedNodeChanged(FileNode? value)
    {
        if (value is null || value.Scanned is null)
        {
            HasSelection = false;
            SelectedName = "—";
            SelectedPath = "—";
            SelectedSize = "—";
            SelectedCategory = "—";
            SelectedLocation = "—";
            SelectedUpgrade = "—";
            SelectedUninstall = "—";
            return;
        }

        HasSelection = true;
        SelectedName = value.Name;
        SelectedPath = value.RelativePath;
        SelectedSize = FileNode.FormatSize(value.Scanned.Size);
        SelectedCategory = CategoryText(value.Scanned.Category);
        var (location, upgrade, uninstall) = Recommend(value.Scanned.Category);
        SelectedLocation = location;
        SelectedUpgrade = upgrade;
        SelectedUninstall = uninstall;
    }

    private static string CategoryText(FileCategory category) => category switch
    {
        FileCategory.Application => "应用程序",
        FileCategory.Library => "程序库",
        FileCategory.Configuration => "配置文件",
        FileCategory.Data => "数据",
        FileCategory.Log => "日志",
        FileCategory.Debug => "调试符号",
        FileCategory.Runtime => "运行时",
        FileCategory.Native => "原生库",
        _ => "未知",
    };

    private static (string Location, string Upgrade, string Uninstall) Recommend(FileCategory category) =>
        category switch
        {
            FileCategory.Configuration => ("应用目录 {app}", "升级时保留现有文件", "卸载时保留"),
            FileCategory.Data => ("应用目录 {app}", "升级时保留现有文件", "卸载时保留"),
            FileCategory.Log or FileCategory.Debug => ("应用目录 {app}", "升级时删除", "卸载时删除"),
            _ => ("应用目录 {app}", "每次升级覆盖", "卸载时删除"),
        };
}
