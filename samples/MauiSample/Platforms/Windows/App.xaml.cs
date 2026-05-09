using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace MediaEmbedKit.Mpv.Samples.Maui.WinUI
{
    /// <summary>
    /// 表示 MAUI Windows 範例應用程式。
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// 建立 MAUI Windows 範例應用程式。
        /// </summary>
        /// <returns>已設定的 MAUI 應用程式。</returns>
        protected override MauiApp CreateMauiApp()
        {
            return MauiProgram.CreateMauiApp();
        }
    }
}
