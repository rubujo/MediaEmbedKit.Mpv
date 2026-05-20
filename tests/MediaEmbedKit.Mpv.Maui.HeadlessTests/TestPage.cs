using System;
using Microsoft.Maui.Controls;

namespace MediaEmbedKit.Mpv.Maui.HeadlessTests;

/// <summary>
/// 在頁面顯示時跑完所有 無頭測試，再退出應用程式。
/// </summary>
public sealed class TestPage : ContentPage
{
    /// <summary>
    /// 紀錄頁面是否已執行過測試，避免 OnAppearing 重入時重覆執行。
    /// </summary>
    private bool _ran;

    /// <summary>
    /// 初始化 <see cref="TestPage"/> 類別的新執行個體。
    /// </summary>
    public TestPage()
    {
        Title = "Tests";
    }

    /// <summary>
    /// 在頁面顯示時呼叫 <see cref="TestRunner.RunAll"/>，再透過 <see cref="Application.Quit"/>
    /// 結束 host 應用程式。
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_ran)
        {
            return;
        }

        _ran = true;
        try
        {
            TestRunner.RunAll();
        }
        catch (Exception ex)
        {
            TestRunner.Failures.Add("OnAppearing 未預期例外：" + ex.Message);
        }
        finally
        {
            TestRunner.WriteSummary();
            // 直接 Environment.Exit 避開 MauiWinUIApplication 自然 tear-down 在 Windows
            // process 退出時觸發的 stowed exception（0xC000027B），確保 exit code 反映
            // 測試結果而非 host 關閉時的 race condition。
            Environment.Exit(TestRunner.Failures.Count > 0 ? 1 : 0);
        }
    }
}
