using System;
using System.Threading;
using Microsoft.Maui.Controls;

namespace MediaEmbedKit.Mpv.Samples.Maui;

/// <summary>
/// 表示 .NET MAUI 範例應用程式。資源字典定義在 <c>App.xaml</c>。
/// </summary>
public sealed partial class App : Application
{
    /// <summary>
    /// 初始化 <see cref="App"/> 類別的新執行個體並載入 XAML 資源。
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 建立 MAUI 範例應用程式的主要視窗。
    /// </summary>
    /// <param name="activationState">
    /// 平台提供的啟用狀態。
    /// </param>
    /// <returns>
    /// 包含主要頁面的 MAUI 視窗。
    /// </returns>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        MainPage page = new MainPage();
        Window window = new Window(page)
        {
            Title = "MediaEmbedKit.Mpv MAUI Sample",
            Width = SampleRuntime.SampleWindowWidth,
            Height = SampleRuntime.SampleWindowHeight
        };
        int destroyed = 0;
        window.Destroying += async delegate (object? sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref destroyed, 1) != 0)
            {
                return;
            }

            await page.PrepareCloseAsync().ConfigureAwait(true);
            page.Dispose();
        };

        return window;
    }
}
