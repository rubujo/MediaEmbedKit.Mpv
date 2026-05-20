using System;
using Microsoft.Maui.Controls;

namespace MediaEmbedKit.Mpv.Maui.HeadlessTests;

/// <summary>
/// MAUI headless 測試的 <see cref="Application"/>；建立一個 <see cref="TestPage"/>，
/// 在頁面顯示時跑完所有測試後立即關閉應用程式。
/// </summary>
public sealed class TestApp : Application
{
    /// <summary>
    /// 建立一個含 <see cref="TestPage"/> 的 Window。<see cref="TestPage.OnAppearing"/>
    /// 會在 UI thread 跑完所有測試後寫入 exit code 並關閉應用程式。
    /// </summary>
    /// <param name="activationState">
    /// 平台提供的啟用狀態。
    /// </param>
    /// <returns>
    /// 含測試頁面的 Window。
    /// </returns>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new TestPage())
        {
            Title = "MediaEmbedKit.Mpv.Maui.HeadlessTests"
        };
    }
}
