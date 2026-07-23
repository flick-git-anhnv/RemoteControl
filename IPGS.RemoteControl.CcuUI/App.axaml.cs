using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IPGS.RemoteControl.CcuUI.Views;

namespace IPGS.RemoteControl.CcuUI;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new ConnectionEntryWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
