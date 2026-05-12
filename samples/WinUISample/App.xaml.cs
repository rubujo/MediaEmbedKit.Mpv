using Microsoft.UI.Xaml;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.WinUI
{
    /// <summary>
    /// 表示 WinUI 3 範例應用程式。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 初始化 <see cref="App"/> 類別的新執行個體。
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 保存範例應用程式的主要視窗。
        /// </summary>
        private Window? _window;

        /// <summary>
        /// 在應用程式啟動時建立並顯示主要視窗。
        /// </summary>
        /// <param name="args">WinUI 啟動事件資料。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            LaunchMainWindow();
        }

        /// <summary>
        /// 建立並顯示 WinUI 3 範例主要視窗。
        /// </summary>
        internal void LaunchMainWindow()
        {
            if (_window != null)
            {
                return;
            }

            SampleRuntime.InstallOrUpdateAsync().GetAwaiter().GetResult();
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
