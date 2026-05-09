using System;
using System.Windows;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.Wpf
{
    /// <summary>
    /// 表示 WPF 範例應用程式。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 在 WPF 範例啟動時安裝執行階段並建立主要視窗。
        /// </summary>
        /// <param name="e">啟動事件資料。</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                SampleRuntime.InstallOrUpdateAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "mpv runtime", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
