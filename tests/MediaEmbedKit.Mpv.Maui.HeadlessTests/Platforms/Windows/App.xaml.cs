using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace MediaEmbedKit.Mpv.Maui.HeadlessTests.WinUI;

/// <summary>
/// 表示 MAUI 無頭測試的 Windows host 應用程式；委派至 <see cref="MauiProgram.CreateMauiApp"/>。
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// 建立 MAUI 應用程式以承載 無頭測試。
    /// </summary>
    /// <returns>
    /// 已設定的 MAUI 應用程式。
    /// </returns>
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}
