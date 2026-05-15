using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace MediaEmbedKit.Mpv.WinUI.HeadlessTests;

/// <summary>
/// WinUI 3 headless 測試的應用程式 host。<see cref="OnLaunched"/> 在 UI thread
/// 排程跑完所有測試案例後立即 <see cref="Application.Exit"/>，不顯示任何視窗。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 初始化 <see cref="App"/> 類別的新執行個體，載入 <c>App.xaml</c>。
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 在 UI thread 跑完所有測試，並把結果寫入 <see cref="TestRunner.Failures"/>。
    /// </summary>
    /// <param name="args">WinUI 啟動事件資料。</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue queue = DispatcherQueue.GetForCurrentThread();
        queue.TryEnqueue(() =>
        {
            try
            {
                TestRunner.RunAll();
            }
            catch (Exception ex)
            {
                TestRunner.Failures.Add("OnLaunched 未預期例外：" + ex.Message);
            }
            finally
            {
                // 直接 Environment.Exit 避開 WinUI Application.Exit 自然 tear-down 在
                // Windows process 退出時可能觸發的 stowed exception（0xC000027B），確保
                // exit code 反映測試結果而非 host 關閉時的 race condition。
                Environment.Exit(TestRunner.Failures.Count > 0 ? 1 : 0);
            }
        });
    }
}
