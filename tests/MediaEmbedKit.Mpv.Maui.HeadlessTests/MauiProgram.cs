using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace MediaEmbedKit.Mpv.Maui.HeadlessTests;

/// <summary>
/// 提供 MAUI 無頭測試的入口；不註冊 MpvViewHandler，由 OnAppearing 直接驗證
/// <see cref="MediaEmbedKit.Mpv.Maui.Windows.MpvView"/> 的 BindableProperty 與 命令。
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// 建立 無頭測試的 MAUI 應用程式。
    /// </summary>
    /// <returns>
    /// 已設定的 MAUI 應用程式。
    /// </returns>
    public static MauiApp CreateMauiApp()
    {
        return MauiApp.CreateBuilder()
            .UseMauiApp<TestApp>()
            .Build();
    }
}
