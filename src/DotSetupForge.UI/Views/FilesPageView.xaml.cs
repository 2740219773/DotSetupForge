using System.Windows;
using System.Windows.Controls;
using DotSetupForge.UI.Models;
using DotSetupForge.UI.ViewModels;

namespace DotSetupForge.UI.Views;

/// <summary>Interaction logic for FilesPageView.xaml。</summary>
public partial class FilesPageView : UserControl
{
    public FilesPageView()
    {
        InitializeComponent();
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is FilesPageViewModel vm)
        {
            vm.SelectedNode = e.NewValue as FileNode;
        }
    }

    private void FileCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FilesPageViewModel vm && sender is CheckBox { DataContext: FileNode node } checkBox)
        {
            // IsThreeState 让目录能显示部分选中；点击 true 后 WPF 可能产生 null。
            // null 在交互中统一为“全部取消”，绝不留在无法操作的中间状态。
            node.ApplyUserState(checkBox.IsChecked);
            vm.ToggleIncludeCommand.Execute(node);
        }
    }

    private void FileNode_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is not FilesPageViewModel vm || sender is not FrameworkElement { DataContext: FileNode node })
        {
            return;
        }

        // 右键是“只切换当前节点”：目录不递归，文件则精确写入排除规则。
        node.ApplyNodeOnlyState(node.CheckedState != true);
        vm.SelectedNode = node;
        vm.ToggleIncludeCommand.Execute(node);
        e.Handled = true;
    }
}
