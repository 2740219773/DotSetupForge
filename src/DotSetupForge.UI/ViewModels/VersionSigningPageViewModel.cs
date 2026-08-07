using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.Services;

namespace DotSetupForge.UI.ViewModels;

/// <summary>版本与签名页：输出文件名预览 + 签名设置。</summary>
public partial class VersionSigningPageViewModel : ObservableObject, IProjectPageViewModel
{
    private readonly IDialogService _dialogs;
    private EditableProject? _project;

    public VersionSigningPageViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public EditableProject? Project => _project;

    /// <summary>输出文件名预览（用当前产品名/版本替换模板变量）。</summary>
    public string OutputFileNamePreview
    {
        get
        {
            if (_project is null)
            {
                return string.Empty;
            }

            return _project.OutputFileName
                .Replace("{ProductName}", _project.ProductName)
                .Replace("{Version}", _project.Version);
        }
    }

    public string OutputPathPreview
    {
        get
        {
            if (_project is null)
            {
                return string.Empty;
            }

            var dir = _project.OutputDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                dir = "./dist";
            }

            return Path.Combine(dir, OutputFileNamePreview);
        }
    }

    public void Bind(EditableProject project)
    {
        _project = project;
        OnPropertyChanged(nameof(Project));
        OnPropertyChanged(nameof(OutputFileNamePreview));
        OnPropertyChanged(nameof(OutputPathPreview));

        project.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditableProject.ProductName) or nameof(EditableProject.Version)
                or nameof(EditableProject.OutputFileName) or nameof(EditableProject.OutputDirectory))
            {
                OnPropertyChanged(nameof(OutputFileNamePreview));
                OnPropertyChanged(nameof(OutputPathPreview));
            }
        };
    }

    [RelayCommand]
    private void BrowseCertificate()
    {
        if (_project is null)
        {
            return;
        }

        var path = _dialogs.PickFile("选择证书文件", "证书 (*.pfx;*.p12)|*.pfx;*.p12|所有文件 (*.*)|*.*");
        if (!string.IsNullOrEmpty(path))
        {
            _project.CertificatePath = path;
        }
    }

    [RelayCommand]
    private void PickOutputDirectory()
    {
        if (_project is null)
        {
            return;
        }

        var dir = _dialogs.PickFolder("选择输出目录");
        if (!string.IsNullOrEmpty(dir))
        {
            _project.OutputDirectory = dir;
        }
    }
}
