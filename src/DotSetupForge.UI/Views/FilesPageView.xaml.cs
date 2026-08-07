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
        if (DataContext is FilesPageViewModel vm && sender is CheckBox { DataContext: FileNode node })
        {
            vm.ToggleIncludeCommand.Execute(node);
        }
    }
}
