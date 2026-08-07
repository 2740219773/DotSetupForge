using System.Windows;
using DotSetupForge.UI.Services;
using DotSetupForge.UI.ViewModels;

namespace DotSetupForge.UI;

/// <summary>Interaction logic for App.xaml。</summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dialogs = new DialogService();
        var mainViewModel = new MainViewModel(dialogs);

        var window = new MainWindow
        {
            DataContext = mainViewModel,
        };
        MainWindow = window;
        window.Show();
    }
}
