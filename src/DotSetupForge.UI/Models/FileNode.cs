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

    /// <summary>CheckBox 绑定（TwoWay）。</summary>
    public bool? CheckedState
    {
        get => _checkedState;
        set
        {
            if (_updating)
            {
                return;
            }

            // 用户点击：向下级联，然后聚合父级
            _checkedState = value;
            OnPropertyChanged();

            if (value is not null)
            {
                foreach (var child in Children)
                {
                    child.SetState(value.Value, cascade: true);
                }
            }

            Parent?.RefreshState();
        }
    }

    /// <summary>程序化设置状态（可级联、不触发父级聚合）。</summary>
    public void SetState(bool value, bool cascade)
    {
        _updating = true;
        _checkedState = value;
        OnPropertyChanged();
        _updating = false;

        if (cascade)
        {
            foreach (var child in Children)
            {
                child.SetState(value, cascade: true);
            }
        }
    }

    /// <summary>从子节点重新聚合自身状态，并向上传播。</summary>
    public void RefreshState()
    {
        if (Children.Count == 0)
        {
            return;
        }

        var checkedCount = Children.Count(c => c.CheckedState == true);
        bool? state = checkedCount switch
        {
            0 => false,
            _ when checkedCount == Children.Count => true,
            _ => null,
        };

        _updating = true;
        _checkedState = state;
        OnPropertyChanged();
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
