using Microsoft.Win32;

namespace DotSetupForge.UI.Services;

/// <summary>对话框服务：GUI 项目只依赖本接口，便于以后替换/测试。</summary>
public interface IDialogService
{
    /// <summary>选择文件夹，取消返回 null。</summary>
    string? PickFolder(string description);

    /// <summary>选择文件，取消返回 null。</summary>
    string? PickFile(string title, string filter);

    /// <summary>确认对话框，返回是否确认。</summary>
    bool Confirm(string message, string title);

    /// <summary>提示信息。</summary>
    void Info(string message, string title);

    /// <summary>错误提示。</summary>
    void Error(string message, string title);
}

/// <summary>WPF 实现。</summary>
public sealed class DialogService : IDialogService
{
    public string? PickFolder(string description)
    {
        var dialog = new OpenFolderDialog
        {
            Title = description,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string message, string title) =>
        System.Windows.MessageBox.Show(
            message, title,
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.OK;

    public void Info(string message, string title) =>
        System.Windows.MessageBox.Show(
            message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);

    public void Error(string message, string title) =>
        System.Windows.MessageBox.Show(
            message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
}
