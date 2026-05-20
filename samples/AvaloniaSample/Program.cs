using System;
using Avalonia;

namespace MediaEmbedKit.Mpv.Samples.Avalonia;

/// <summary>
/// 提供 Avalonia 範例應用程式的進入點。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 啟動 Avalonia 範例應用程式。
    /// </summary>
    /// <param name="args">
    /// 命令列引數。
    /// </param>
    [STAThread]
    private static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// 建立 Avalonia 應用程式建置器。
    /// </summary>
    /// <returns>
    /// 已設定桌面平台偵測的 Avalonia 應用程式建置器。
    /// </returns>
    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect();
    }
}
