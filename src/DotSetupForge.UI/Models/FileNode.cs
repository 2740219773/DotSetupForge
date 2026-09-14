using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DotSetupForge.Core.Analysis;

namespace DotSetupForge.UI.Models;

/// <summary>
/// 文件树节点：目录或文件，支持三态勾选（父子联动）。
/// CheckedState：true=包含，false=排除，null=子级部分包含（仅目录）。
/// </summary>
public partial class FileNode : ObservableObject
{
    private bool? _checkedState;
    private bool _updating;

    public FileNode(string name, string relativePath, bool isDirectory, FileNode? parent)
    {
        Name = name;
        RelativePath = relativePath;
        IsDirectory = isDirectory;
        Parent = parent;
        Children = [];
    }

    public string Name { get; }

    /// <summary>相对源目录的路径；目录用 "Data"（无尾部斜杠）。</summary>
    public string RelativePath { get; }

    public bool IsDirectory { get; }

    public FileNode? Parent { get; }

    public ObservableCollection<FileNode> Children { get; }

    /// <summary>文件节点对应的扫描数据。</summary>
    public ScannedFile? Scanned { get; set; }

    /// <summary>是否被排除规则命中。</summary>
    public bool RuleExcluded { get; set; }

    public string DisplaySize => Scanned is null ? string.Empty : FormatSize(Scanned.Size);

    /// <summary>TreeList 状态列：目录只展示汇总，避免子项修改造成父级操作状态切换。</summary>
    public string SelectionSummary => IsDirectory
        ? $"{Children.Sum(child => child.IncludedFileCount)} / {EnumerateFiles().Count()} 包含"
        : CheckedState == true ? "包含" : "排除";

    /// <summary>文件行的单项操作文字。</summary>
    public string FileActionText => CheckedState == true ? "排除" : "包含";

    private int IncludedFileCount => IsDirectory
        ? Children.Sum(child => child.IncludedFileCount)
        : CheckedState == true ? 1 : 0;

    /// <summary>CheckBox 的唯一状态入口。用户操作会向下覆盖，并向上聚合。</summary>
    public bool? CheckedState
    {
        get => _checkedState;
        set
        {
            if (_updating)
            {
                return;
            }

            // null 仅用于目录的聚合显示，不是一个可持久化的用户选择。
            if (value is bool included)
            {
                ApplyUserState(included);
            }
        }
    }

    /// <summary>程序化设置状态（可级联、不触发父级聚合）。</summary>
    public void SetState(bool value, bool cascade)
    {
        _updating = true;
        _checkedState = value;
        OnPropertyChanged(nameof(CheckedState));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(FileActionText));
        _updating = false;

        if (cascade)
        {
            foreach (var child in Children)
            {
                child.SetState(value, cascade: true);
            }
        }
    }

    /// <summary>处理用户选择：目录递归覆盖子树，绝不改变父节点的操作状态。</summary>
    public void ApplyUserState(bool value)
    {
        SetState(value, cascade: true);
    }

    /// <summary>把三态控件的用户点击归一为明确选择；部分选中状态点击时按“全部取消”处理。</summary>
    public void ApplyUserState(bool? value) => ApplyUserState(value ?? false);

    /// <summary>右键单节点操作：只更新当前节点，不级联子节点，也不影响父节点。</summary>
    public void ApplyNodeOnlyState(bool value) => SetState(value, cascade: false);

    /// <summary>从子节点重新聚合自身状态，并向上传播。</summary>
    public void RefreshState()
    {
        if (Children.Count == 0)
        {
            return;
        }

        bool? state = Children.All(c => c.CheckedState == true)
            ? true
            : Children.All(c => c.CheckedState == false)
                ? false
                : null;

        _updating = true;
        _checkedState = state;
        OnPropertyChanged(nameof(CheckedState));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(FileActionText));
        _updating = false;

        Parent?.RefreshState();
    }

    /// <summary>收集所有文件节点（含子级）。</summary>
    public IEnumerable<FileNode> EnumerateFiles()
    {
        if (!IsDirectory)
        {
            yield return this;
            yield break;
        }

        foreach (var child in Children)
        {
            foreach (var node in child.EnumerateFiles())
            {
                yield return node;
            }
        }
    }

    public static string FormatSize(long size)
    {
        if (size < 1024)
        {
            return $"{size} B";
        }

        if (size < 1024 * 1024)
        {
            return $"{size / 1024.0:F1} KB";
        }

        if (size < 1024L * 1024 * 1024)
        {
            return $"{size / (1024.0 * 1024):F1} MB";
        }

        return $"{size / (1024.0 * 1024 * 1024):F1} GB";
    }
}
