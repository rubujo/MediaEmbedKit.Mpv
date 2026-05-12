using Microsoft.Maui.Controls;
using System;

namespace MediaEmbedKit.Mpv.Samples.Maui
{
    /// <summary>
    /// 表示 .NET MAUI 範例應用程式。
    /// </summary>
    public sealed class App : Application
    {
        /// <summary>
        /// 建立 MAUI 範例應用程式的主要視窗。
        /// </summary>
        /// <param name="activationState">平台提供的啟用狀態。</param>
        /// <returns>包含主要頁面的 MAUI 視窗。</returns>
        protected override Window CreateWindow(IActivationState? activationState)
        {
            MainPage page = new MainPage();
            Window window = new Window(page)
            {
                Title = "MediaEmbedKit.Mpv MAUI Sample",
                Width = SampleRuntime.SampleWindowWidth,
                Height = SampleRuntime.SampleWindowHeight
            };
            window.Destroying += delegate (object? sender, EventArgs e)
            {
                page.Dispose();
            };

            return window;
        }
    }
}
