using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.WinUI;
using Microsoft.UI.Xaml;

namespace MediaEmbedKit.Mpv.WinUI.HeadlessTests;

/// <summary>
/// 驗證 <see cref="MpvWinUiPlayer"/> 公開 DependencyProperty 與 命令。WinUI 3
/// 控制項需要 Application context 才能建構，因此 entry point 由 WinUI SDK 自動產生並
/// 啟動 <see cref="App"/>；本類別由 <see cref="App.OnLaunched"/> 在 UI thread 呼叫
/// <see cref="RunAll"/>。本套件**不**啟動真實 libmpv，只覆蓋 WinUI 屬性系統、CLR
/// 包裝層、命令 守備路徑與 Dispose 重入。
/// </summary>
internal static class TestRunner
{
    /// <summary>
    /// 累計失敗測試的訊息；由 host 處理序在 <see cref="App"/> Exit 後依此決定 exit code。
    /// </summary>
    internal static readonly List<string> Failures = new List<string>();

    /// <summary>
    /// 由 <see cref="App.OnLaunched"/> 呼叫；依序執行所有測試案例。每案獨立 try/catch，
    /// 互相不影響，最後寫入 <see cref="Failures"/>。
    /// </summary>
    internal static void RunAll()
    {
        ConfigureConsoleEncoding();
        Run("DependencyProperty 全部已註冊", VerifyDependencyPropertiesRegistered);
        Run("DependencyProperty 預設值正確", VerifyDefaultValues);
        Run("Source / Position / Volume / IsPaused / IsMuted CLR setter round-trip", VerifyReadWriteRoundTrip);
        Run("Duration / PlaybackState 模擬唯讀（外部寫入還原）", VerifyReadOnlyDependencyProperties);
        Run("OverlayContent / IsOverlayOpen 預設與 round-trip", VerifyOverlayProperties);
        Run("Play / Pause / Stop / TogglePause / ToggleMute 命令 可取得且 CanExecute 預設 false（無 player）", VerifyCommandsExposed);
        Run("命令.Execute 在無 player 時不擲例外", VerifyCommandsSafeWithoutPlayer);
        Run("Dispose 可重入（無 player 與重複 Dispose 都不擲例外）", VerifyDisposeReentrant);

        if (Failures.Count == 0)
        {
            Console.WriteLine("WinUI 無頭測試完成：全部通過。");
        }
        else
        {
            Console.Error.WriteLine("WinUI 無頭測試失敗：");
            foreach (string failure in Failures)
            {
                Console.Error.WriteLine("  - " + failure);
            }

            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// 將測試輸出固定為 UTF-8，避免 Windows CI 將中文測試名稱轉成問號。
    /// </summary>
    private static void ConfigureConsoleEncoding()
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
    /// 驗證 9 個 DependencyProperty 註冊存在（不依賴實例化）。
    /// </summary>
    private static void VerifyDependencyPropertiesRegistered()
    {
        DependencyProperty[] properties = new[]
        {
            MpvWinUiPlayer.OverlayContentProperty,
            MpvWinUiPlayer.IsOverlayOpenProperty,
            MpvWinUiPlayer.SourceProperty,
            MpvWinUiPlayer.PositionProperty,
            MpvWinUiPlayer.DurationProperty,
            MpvWinUiPlayer.VolumeProperty,
            MpvWinUiPlayer.IsPausedProperty,
            MpvWinUiPlayer.IsMutedProperty,
            MpvWinUiPlayer.PlaybackStateProperty
        };
        for (int index = 0; index < properties.Length; index++)
        {
            Assert(properties[index] != null, "DependencyProperty index=" + index + " 不可為 null");
        }
    }

    /// <summary>
    /// 驗證實例化後每個 DP 預設值符合 spec。
    /// </summary>
    private static void VerifyDefaultValues()
    {
        MpvWinUiPlayer player = new MpvWinUiPlayer();
        try
        {
            Assert(player.Source == null, "Source 預設應為 null");
            Assert(player.Position == TimeSpan.Zero, "Position 預設應為 TimeSpan.Zero");
            Assert(player.Volume == 100.0, "Volume 預設應為 100.0");
            Assert(player.IsPaused == false, "IsPaused 預設應為 false");
            Assert(player.IsMuted == false, "IsMuted 預設應為 false");
            Assert(player.Duration == TimeSpan.Zero, "Duration 預設應為 TimeSpan.Zero");
            Assert(player.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 預設應為 Idle");
            Assert(player.IsOverlayOpen == true, "IsOverlayOpen 預設應為 true");
            Assert(player.OverlayContent == null, "OverlayContent 預設應為 null");
            Assert(player.Player == null, "尚未 Initialize 時 Player 應為 null");
        }
        finally
        {
            player.Dispose();
        }
    }

    /// <summary>
    /// 驗證可讀寫 DP 寫入後讀回相符。
    /// </summary>
    private static void VerifyReadWriteRoundTrip()
    {
        MpvWinUiPlayer player = new MpvWinUiPlayer();
        try
        {
            player.Source = "https://example.invalid/file.mp4";
            Assert(player.Source == "https://example.invalid/file.mp4", "Source round-trip 失敗");

            player.Volume = 80.0;
            Assert(player.Volume == 80.0, "Volume 80 寫入應讀回 80");

            player.IsPaused = true;
            Assert(player.IsPaused == true, "IsPaused true 寫入應讀回 true");

            player.IsMuted = true;
            Assert(player.IsMuted == true, "IsMuted true 寫入應讀回 true");

            player.Position = TimeSpan.FromSeconds(42);
            Assert(player.Position == TimeSpan.FromSeconds(42), "Position 42s 寫入應讀回 42s");
        }
        finally
        {
            player.Dispose();
        }
    }

    /// <summary>
    /// 驗證 Duration / PlaybackState 模擬唯讀：外部 SetValue 應還原至先前值。
    /// </summary>
    private static void VerifyReadOnlyDependencyProperties()
    {
        MpvWinUiPlayer player = new MpvWinUiPlayer();
        try
        {
            player.SetValue(MpvWinUiPlayer.DurationProperty, TimeSpan.FromHours(1));
            Assert(player.Duration == TimeSpan.Zero, "Duration 外部 SetValue 應被還原為預設值");

            player.SetValue(MpvWinUiPlayer.PlaybackStateProperty, MpvPlaybackState.Playing);
            Assert(player.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 外部 SetValue 應被還原為預設值");
        }
        finally
        {
            player.Dispose();
        }
    }

    /// <summary>
    /// 驗證 OverlayContent / IsOverlayOpen 預設與 round-trip。
    /// </summary>
    private static void VerifyOverlayProperties()
    {
        MpvWinUiPlayer player = new MpvWinUiPlayer();
        try
        {
            player.IsOverlayOpen = false;
            Assert(player.IsOverlayOpen == false, "IsOverlayOpen false round-trip 失敗");

            Microsoft.UI.Xaml.Controls.Border overlay = new Microsoft.UI.Xaml.Controls.Border();
            player.OverlayContent = overlay;
            Assert(ReferenceEquals(player.OverlayContent, overlay), "OverlayContent round-trip 失敗");
        }
        finally
        {
            player.Dispose();
        }
    }

    /// <summary>
    /// 驗證 5 個命令 都已暴露且 CanExecute 在無 player 時為 false。
    /// </summary>
    private static void VerifyCommandsExposed()
    {
        MpvWinUiPlayer player = new MpvWinUiPlayer();
        try
        {
            ICommand[] commands = new[]
            {
                player.PlayCommand, player.PauseCommand, player.StopCommand,
                player.TogglePauseCommand, player.ToggleMuteCommand
            };
            for (int index = 0; index < commands.Length; index++)
            {
                Assert(commands[index] != null, "Command index=" + index + " 不可為 null");
                Assert(commands[index].CanExecute(null) == false,
                    "Command index=" + index + " 無 player 時 CanExecute 應為 false");
            }
        }
        finally
        {
            player.Dispose();
        }
    }

    /// <summary>
    /// 驗證在無 player 情況下呼叫 命令.Execute 不擲例外（守備路徑）。
    /// </summary>
    private static void VerifyCommandsSafeWithoutPlayer()
    {
        MpvWinUiPlayer player = new MpvWinUiPlayer();
        try
        {
            player.PlayCommand.Execute(null);
            player.PauseCommand.Execute(null);
            player.StopCommand.Execute(null);
            player.TogglePauseCommand.Execute(null);
            player.ToggleMuteCommand.Execute(null);
        }
        finally
        {
            player.Dispose();
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvWinUiPlayer.Dispose"/> 在無 player 與重複呼叫情境下都不擲例外。
    /// </summary>
    private static void VerifyDisposeReentrant()
    {
        MpvWinUiPlayer player = new MpvWinUiPlayer();
        player.Dispose();
        player.Dispose();
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
