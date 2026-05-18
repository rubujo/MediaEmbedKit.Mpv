using MediaEmbedKit.Mpv.Maui.Windows;
using Microsoft.Maui.Hosting;

namespace MediaEmbedKit.Mpv.Samples.Maui;

/// <summary>
/// 提供 .NET MAUI 範例應用程式的建置入口。
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// 建立 MAUI 範例應用程式。
    /// </summary>
    /// <returns>已設定 MediaEmbedKit.Mpv handler 的 MAUI 應用程式。</returns>
    public static MauiApp CreateMauiApp()
    {
        return MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .UseMediaEmbedKitMpv()
            .Build();
    }
}
