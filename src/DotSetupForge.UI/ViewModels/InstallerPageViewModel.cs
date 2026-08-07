using CommunityToolkit.Mvvm.ComponentModel;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.Services;

namespace DotSetupForge.UI.ViewModels;

/// <summary>安装设置页：安装范围 / 目录 / 快捷方式 / 升级选项。</summary>
public partial class InstallerPageViewModel : ObservableObject, IProjectPageViewModel
{
    private readonly IDialogService _dialogs;
    private EditableProject? _project;

    public InstallerPageViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public EditableProject? Project => _project;

    [ObservableProperty]
    private string _scopeHint = "安装到 Program Files，需要管理员权限；可同时安装系统级 .NET Runtime。";

    public void Bind(EditableProject project)
    {
        _project = project;
        OnPropertyChanged(nameof(Project));
        UpdateScopeHint();
    }

    /// <summary>安装范围变化时更新提示（由 RadioButton 切换后调用）。</summary>
    public void UpdateScopeHint()
    {
        if (_project is null)
        {
            return;
        }

        ScopeHint = _project.InstallScope == "Machine"
            ? "安装到 Program Files，需要管理员权限；可同时安装系统级 .NET Runtime。"
            : "安装到当前用户目录，通常不需要管理员权限；若需安装系统 Runtime 仍可能提权。";
    }

    /// <summary>默认安装目录模板。</summary>
    public string DefaultInstallDirectory =>
        string.IsNullOrEmpty(_project?.Publisher)
            ? "{autopf}\\{Name}"
            : "{autopf}\\{Publisher}\\{Name}";
}
