using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Maui.Windows;
using Microsoft.Maui.Controls;

namespace MediaEmbedKit.Mpv.Maui.HeadlessTests;

/// <summary>
/// 驗證 <see cref="MpvView"/> 公開 BindableProperty 與 命令。MAUI Windows 的
/// VisualElement 靜態建構子相依 WinUI host，因此入口採 <see cref="WinUI.App"/>
/// (<see cref="MauiWinUIApplication"/>) 啟動完整 MAUI Windows host，再由
/// <see cref="TestPage.OnAppearing"/> 在 UI thread 呼叫 <see cref="RunAll"/>。
/// 本套件**不**啟動真實 libmpv，也不註冊 <c>MpvViewHandler</c>，只覆蓋 BindableObject
/// 屬性系統、CLR 包裝層、命令 守備路徑與唯讀 BindableProperty 的外部寫入語意。
/// </summary>
internal static class TestRunner
{
    /// <summary>
    /// 累計失敗測試的訊息；由 host 處理序在 <see cref="WriteSummary"/> 後決定 exit code。
    /// </summary>
    internal static readonly List<string> Failures = new List<string>();

    /// <summary>
    /// 依序執行所有測試案例。每案獨立 try/catch，互相不影響，最後寫入 <see cref="Failures"/>。
    /// </summary>
    internal static void RunAll()
    {
        ConfigureConsoleEncoding();
        Run("BindableProperty 全部已註冊", VerifyBindablePropertiesRegistered);
        Run("BindableProperty 預設值正確", VerifyDefaultValues);
        Run("Source / Position / Volume / IsPaused / IsMuted CLR setter round-trip", VerifyReadWriteRoundTrip);
        Run("Duration / PlaybackState 唯讀 BindableProperty（外部 SetValue 應被拒）", VerifyReadOnlyBindableProperties);
        Run("OverlayView / IsOverlayOpen 預設與 round-trip", VerifyOverlayProperties);
        Run("Play / Pause / Stop / TogglePause / ToggleMute 命令 可取得且 CanExecute 預設 false（無 player）", VerifyCommandsExposed);
        Run("命令.Execute 在無 player 時不擲例外", VerifyCommandsSafeWithoutPlayer);
        Run("Binding StringFormat 對 PlaybackState 正常運作", VerifyPlaybackStateBindingFormat);
    }

    /// <summary>
    /// 將測試輸出固定為 UTF-8，避免 Windows CI 將中文測試名稱轉成問號。
    /// </summary>
    private static void ConfigureConsoleEncoding()
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    /// <summary>
    /// 將測試結果摘要寫到 stdout / stderr 並設定 <see cref="Environment.ExitCode"/>。
    /// </summary>
    internal static void WriteSummary()
    {
        if (Failures.Count == 0)
        {
            Console.WriteLine("MAUI 無頭測試完成：全部通過。");
            Environment.ExitCode = 0;
            return;
        }

        Console.Error.WriteLine("MAUI 無頭測試失敗：");
        foreach (string failure in Failures)
        {
            Console.Error.WriteLine("  - " + failure);
        }

        Environment.ExitCode = 1;
    }

    /// <summary>
    /// 執行單一測試並於 stdout 印出結果；失敗時加入 <see cref="Failures"/>，
    /// 不擲例外避免擋住後續測試。
    /// </summary>
    /// <param name="name">
    /// 測試名稱。
    /// </param>
    /// <param name="action">
    /// 測試動作。
    /// </param>
    private static void Run(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine("[PASS] " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[FAIL] " + name + " - " + ex.GetType().Name + ": " + ex.Message);
            Failures.Add(name + ": " + ex.Message);
        }
    }

    /// <summary>
    /// 驗證 10 個 BindableProperty 註冊存在（不依賴實例化）。
    /// </summary>
    private static void VerifyBindablePropertiesRegistered()
    {
        BindableProperty[] properties = new[]
        {
            MpvView.SourceProperty,
            MpvView.OverlayViewProperty,
            MpvView.OverlayContentProperty,
            MpvView.IsOverlayOpenProperty,
            MpvView.PositionProperty,
            MpvView.DurationProperty,
            MpvView.VolumeProperty,
            MpvView.IsPausedProperty,
            MpvView.IsMutedProperty,
            MpvView.PlaybackStateProperty
        };
        for (int index = 0; index < properties.Length; index++)
        {
            Assert(properties[index] != null, "BindableProperty index=" + index + " 不可為 null");
        }
    }

    /// <summary>
    /// 驗證實例化後每個 BindableProperty 預設值符合 spec。
    /// </summary>
    private static void VerifyDefaultValues()
    {
        MpvView view = new MpvView();
        Assert(view.Source == null, "Source 預設應為 null");
        Assert(view.Position == TimeSpan.Zero, "Position 預設應為 TimeSpan.Zero");
        Assert(view.Volume == 100.0, "Volume 預設應為 100.0");
        Assert(view.IsPaused == false, "IsPaused 預設應為 false");
        Assert(view.IsMuted == false, "IsMuted 預設應為 false");
        Assert(view.Duration == TimeSpan.Zero, "Duration 預設應為 TimeSpan.Zero");
        Assert(view.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 預設應為 Idle");
        Assert(view.IsOverlayOpen == true, "IsOverlayOpen 預設應為 true");
        Assert(view.OverlayView == null, "OverlayView 預設應為 null");
        Assert(view.OverlayContent == null, "OverlayContent 預設應為 null");
    }

    /// <summary>
    /// 驗證可讀寫 BindableProperty 寫入後讀回相符。
    /// </summary>
    private static void VerifyReadWriteRoundTrip()
    {
        MpvView view = new MpvView();

        view.Source = "https://example.invalid/file.mp4";
        Assert(view.Source == "https://example.invalid/file.mp4", "Source round-trip 失敗");

        view.Volume = 80.0;
        Assert(view.Volume == 80.0, "Volume 80 寫入應讀回 80");

        view.IsPaused = true;
        Assert(view.IsPaused == true, "IsPaused true 寫入應讀回 true");

        view.IsMuted = true;
        Assert(view.IsMuted == true, "IsMuted true 寫入應讀回 true");

        view.Position = TimeSpan.FromSeconds(42);
        Assert(view.Position == TimeSpan.FromSeconds(42), "Position 42s 寫入應讀回 42s");
    }

    /// <summary>
    /// 驗證 Duration / PlaybackState 由 <see cref="BindableProperty.CreateReadOnly"/> 註冊：
    /// IsReadOnly 旗標為 true、外部 SetValue 不會改動實際值（無論平台版本是否擲例外）。
    /// </summary>
    /// <remarks>
    /// MAUI 各版本對唯讀 BindableProperty 公開 SetValue 的處理不完全一致（有版本擲
    /// InvalidOperationException、有版本 silently no-op），因此這裡只驗證實際值不變的
    /// 不變量，並用 try/catch 容忍可能擲出的例外。
    /// </remarks>
    private static void VerifyReadOnlyBindableProperties()
    {
        MpvView view = new MpvView();
        Assert(MpvView.DurationProperty.IsReadOnly,
            "DurationProperty 應由 CreateReadOnly 註冊（IsReadOnly == true）");
        Assert(MpvView.PlaybackStateProperty.IsReadOnly,
            "PlaybackStateProperty 應由 CreateReadOnly 註冊（IsReadOnly == true）");

        try
        {
            view.SetValue(MpvView.DurationProperty, TimeSpan.FromHours(1));
        }
        catch (InvalidOperationException)
        {
        }

        Assert(view.Duration == TimeSpan.Zero, "Duration 不應被外部 SetValue 改寫");

        try
        {
            view.SetValue(MpvView.PlaybackStateProperty, MpvPlaybackState.Playing);
        }
        catch (InvalidOperationException)
        {
        }

        Assert(view.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 不應被外部 SetValue 改寫");
    }

    /// <summary>
    /// 驗證 OverlayView / IsOverlayOpen 的預設與 round-trip。
    /// </summary>
    private static void VerifyOverlayProperties()
    {
        MpvView view = new MpvView();

        view.IsOverlayOpen = false;
        Assert(view.IsOverlayOpen == false, "IsOverlayOpen false round-trip 失敗");

        Label overlayLabel = new Label { Text = "overlay" };
        view.OverlayView = overlayLabel;
        Assert(ReferenceEquals(view.OverlayView, overlayLabel), "OverlayView round-trip 失敗");
    }

    /// <summary>
    /// 驗證 5 個命令 都已暴露且 CanExecute 在無 player 時為 false。
    /// </summary>
    private static void VerifyCommandsExposed()
    {
        MpvView view = new MpvView();
        ICommand[] commands = new[]
        {
            view.PlayCommand, view.PauseCommand, view.StopCommand,
            view.TogglePauseCommand, view.ToggleMuteCommand
        };
        for (int index = 0; index < commands.Length; index++)
        {
            Assert(commands[index] != null, "Command index=" + index + " 不可為 null");
            Assert(commands[index].CanExecute(null) == false,
                "Command index=" + index + " 無 player 時 CanExecute 應為 false");
        }
    }

    /// <summary>
    /// 驗證在無 player 情況下呼叫 命令.Execute 不擲例外（守備路徑）。
    /// </summary>
    private static void VerifyCommandsSafeWithoutPlayer()
    {
        MpvView view = new MpvView();
        view.PlayCommand.Execute(null);
        view.PauseCommand.Execute(null);
        view.StopCommand.Execute(null);
        view.TogglePauseCommand.Execute(null);
        view.ToggleMuteCommand.Execute(null);
    }

    /// <summary>
    /// 驗證 sample 端使用的 PlaybackState binding + stringFormat 模式
    /// （<see cref="MpvView"/> 作為 BindingContext，Label 綁定 PlaybackState）。
    /// </summary>
    private static void VerifyPlaybackStateBindingFormat()
    {
        MpvView view = new MpvView();
        Label label = new Label();
        label.SetBinding(
            Label.TextProperty,
            new Binding(nameof(MpvView.PlaybackState), stringFormat: "狀態 = {0}"));
        label.BindingContext = view;

        Assert(label.Text == "狀態 = Idle",
            "PlaybackState binding 預設值字串化失敗：實際為 '" + label.Text + "'");
    }

    /// <summary>
    /// 條件斷言；失敗時擲出含訊息的例外。
    /// </summary>
    /// <param name="condition">
    /// 應為 true 的條件。
    /// </param>
    /// <param name="message">
    /// 失敗訊息。
    /// </param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
