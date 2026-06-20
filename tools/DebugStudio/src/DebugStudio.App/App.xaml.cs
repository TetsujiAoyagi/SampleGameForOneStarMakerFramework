using System.Windows;
using DebugStudio.App.Core.Composition;

namespace DebugStudio.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var compositionRoot = new AppCompositionRoot();
        var mainWindow = compositionRoot.CreateMainWindow();
        mainWindow.Show();
    }
}
