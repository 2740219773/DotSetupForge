using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using DotSetupForge.UI.ViewModels;

namespace DotSetupForge.UI.Views;

/// <summary>Interaction logic for BuildPageView.xaml。</summary>
public partial class BuildPageView : UserControl
{
    public BuildPageView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BuildPageViewModel vm)
        {
            vm.LogLines.CollectionChanged += OnLogsChanged;
        }
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[^1]);
        }
    }
}
