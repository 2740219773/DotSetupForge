using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private string _scopeHint = "默认安装到 D:\\Apps，需要管理员权限；目标机没有 D 盘时自动回退到 Program Files。";

    public void Bind(EditableProject project)
    {
        _project = project;
        if (string.IsNullOrWhiteSpace(_project.InstallDirectory))
        {
            _project.InstallDirectory = DefaultInstallDirectory;
        }
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
            ? "默认安装到 D:\\Apps，需要管理员权限；目标机没有 D 盘时自动回退到 Program Files。"
            : "安装到当前用户目录，通常不需要管理员权限；若需安装系统 Runtime 仍可能提权。";
    }

    /// <summary>默认安装目录模板。</summary>
    public string DefaultInstallDirectory =>
        string.IsNullOrWhiteSpace(_project?.Publisher)
            ? $@"D:\Apps\{DefaultProductName}"
            : $@"D:\Apps\{_project.Publisher}\{DefaultProductName}";

    [RelayCommand]
    private void SelectWizardSmallImage() => SelectWizardImage(
        "选择安装向导右上角图标",
        path => _project!.WizardSmallImagePath = path);

    [RelayCommand]
    private void ClearWizardSmallImage()
    {
        if (_project is not null)
        {
            _project.WizardSmallImagePath = string.Empty;
        }
    }

    [RelayCommand]
    private void SelectWizardImage() => SelectWizardImage(
        "选择安装向导欢迎页图片",
        path => _project!.WizardImagePath = path);

    [RelayCommand]
    private void ClearWizardImage()
    {
        if (_project is not null)
        {
            _project.WizardImagePath = string.Empty;
        }
    }

    private void SelectWizardImage(string title, Action<string> assign)
    {
        if (_project is null)
        {
            return;
        }

        var path = _dialogs.PickFile(title, "图片文件 (*.png;*.bmp)|*.png;*.bmp");
        if (!string.IsNullOrWhiteSpace(path))
        {
            assign(path);
        }
    }

    private string DefaultProductName => string.IsNullOrWhiteSpace(_project?.ProductName) ? "Application" : _project.ProductName;
}
