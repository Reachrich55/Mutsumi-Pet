using System.Windows;
using System.Windows.Threading;

namespace MutsuPet;

public partial class App : Application
{
    /// <summary>
    /// 应用启动时创建并显示桌宠主窗口。
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    /// <summary>
    /// 捕获 UI 线程未处理异常并展示简短错误信息。
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"遇到了一点问题：{e.Exception.Message}",
            "Mutsu Pet",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
