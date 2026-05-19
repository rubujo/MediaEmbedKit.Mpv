using System;
using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.WinForms;

namespace MediaEmbedKit.Mpv.WinForms.HeadlessTests;

/// <summary>
/// 在 STA thread 驗證 <see cref="MpvPlayerControl"/> 公開 INPC 屬性與 5 個 Commands。
/// 本套件**不**啟動真實 libmpv，只覆蓋 WinForms 控制項屬性層與 INPC 通知。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 測試執行進入點（必須 STA thread）。
    /// </summary>
    /// <param name="args">命令列引數；目前未使用。</param>
    /// <returns>全部通過時為 0，否則為 1。</returns>
    [STAThread]
    private static int Main(string[] args)
    {
        _ = args;
        ConfigureConsoleEncoding();
        try
        {
            Run("INPC 屬性預設值正確", VerifyDefaultValues);
            Run("可讀寫屬性 setter round-trip 並觸發 PropertyChanged", VerifyReadWriteRoundTrip);
            Run("Source setter round-trip 並觸發 PropertyChanged", VerifySourceRoundTrip);
            Run("PlaylistIndex / Chapter 無 player 時接受無效值且不擲例外", VerifyNavigationInvalidValuesSafeWithoutPlayer);
            Run("Duration / PlaybackState 唯讀（無 public setter）", VerifyReadOnlyProperties);
            Run("5 個 Commands 都已暴露且 CanExecute 在無 player 時為 false", VerifyCommandsExposed);
            Run("無 player 時 Commands.Execute 不擲例外", VerifyCommandsSafeWithoutPlayer);
            Console.WriteLine("WinForms headless 測試完成：全部通過。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
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
    /// 執行單一測試並於 stdout 印出結果。
    /// </summary>
    /// <param name="name">測試名稱。</param>
    /// <param name="action">測試動作。</param>
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
            throw;
        }
    }

    /// <summary>
    /// 驗證所有公開屬性的預設值。
    /// </summary>
    private static void VerifyDefaultValues()
    {
        using MpvPlayerControl control = new MpvPlayerControl();
        Assert(control.Source == null, "Source 預設應為 null");
        Assert(control.Position == TimeSpan.Zero, "Position 預設應為 TimeSpan.Zero");
        Assert(control.Volume == 100.0, "Volume 預設應為 100.0");
        Assert(control.IsPaused == false, "IsPaused 預設應為 false");
        Assert(control.IsMuted == false, "IsMuted 預設應為 false");
        Assert(control.Duration == TimeSpan.Zero, "Duration 預設應為 TimeSpan.Zero");
        Assert(control.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 預設應為 Idle");
        Assert(control.PlaylistIndex == 0, "PlaylistIndex 預設應為 0");
        Assert(control.Chapter == null, "Chapter 預設應為 null");
        Assert(control.Player == null, "尚未 Initialize 時 Player 應為 null");
    }

    /// <summary>
    /// 驗證可讀寫屬性 setter round-trip 並觸發 <see cref="INotifyPropertyChanged.PropertyChanged"/>。
    /// </summary>
    private static void VerifyReadWriteRoundTrip()
    {
        using MpvPlayerControl control = new MpvPlayerControl();
        System.Collections.Generic.List<string> notifications = new System.Collections.Generic.List<string>();
        ((INotifyPropertyChanged)control).PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                notifications.Add(args.PropertyName);
            }
        };

        control.Volume = 80.0;
        Assert(control.Volume == 80.0, "Volume 寫入後應讀回 80");

        control.IsPaused = true;
        Assert(control.IsPaused == true, "IsPaused 寫入後應讀回 true");

        control.IsMuted = true;
        Assert(control.IsMuted == true, "IsMuted 寫入後應讀回 true");

        control.PlaylistIndex = 2;
        Assert(control.PlaylistIndex == 2, "PlaylistIndex 寫入後應讀回 2");

        control.Chapter = 1;
        Assert(control.Chapter == 1, "Chapter 寫入後應讀回 1");

        Assert(notifications.Contains("Volume"), "應收到 Volume PropertyChanged");
        Assert(notifications.Contains("IsPaused"), "應收到 IsPaused PropertyChanged");
        Assert(notifications.Contains("IsMuted"), "應收到 IsMuted PropertyChanged");
        Assert(notifications.Contains("PlaylistIndex"), "應收到 PlaylistIndex PropertyChanged");
        Assert(notifications.Contains("Chapter"), "應收到 Chapter PropertyChanged");

        // 同值寫入不應重複觸發
        int beforeCount = notifications.Count;
        control.Volume = 80.0;
        Assert(notifications.Count == beforeCount, "Volume 同值再寫入不應再觸發 PropertyChanged");
    }

    /// <summary>
    /// 驗證 Source 設值並觸發 PropertyChanged。
    /// </summary>
    private static void VerifySourceRoundTrip()
    {
        using MpvPlayerControl control = new MpvPlayerControl();
        bool gotSourceChange = false;
        ((INotifyPropertyChanged)control).PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == "Source")
            {
                gotSourceChange = true;
            }
        };

        control.Source = "https://example.invalid/file.mp4";
        Assert(control.Source == "https://example.invalid/file.mp4", "Source round-trip 失敗");
        Assert(gotSourceChange, "應收到 Source PropertyChanged");
    }

    /// <summary>
    /// 驗證無 player 時章節與播放清單無效值只更新控制項狀態，不會擲例外或要求 libmpv。
    /// </summary>
    private static void VerifyNavigationInvalidValuesSafeWithoutPlayer()
    {
        using MpvPlayerControl control = new MpvPlayerControl();

        control.Chapter = null;
        Assert(control.Chapter == null, "Chapter 寫入 null 後應維持 null");

        control.Chapter = -1;
        Assert(control.Chapter == -1, "Chapter 寫入負數後應保留控制項本機值");

        control.PlaylistIndex = -1;
        Assert(control.PlaylistIndex == -1, "PlaylistIndex 寫入負數後應保留控制項本機值");
    }

    /// <summary>
    /// 驗證 Duration / PlaybackState 為唯讀（無 public setter）。
    /// </summary>
    private static void VerifyReadOnlyProperties()
    {
        using MpvPlayerControl control = new MpvPlayerControl();
        // 由 PropertyInfo 確認 setter 為 internal/private（沒有 public set）。
        System.Reflection.PropertyInfo? durationProperty = typeof(MpvPlayerControl).GetProperty(nameof(MpvPlayerControl.Duration));
        Assert(durationProperty != null, "Duration property 反射應存在");
        Assert(durationProperty!.GetSetMethod(nonPublic: false) == null, "Duration 不應有 public setter");

        System.Reflection.PropertyInfo? stateProperty = typeof(MpvPlayerControl).GetProperty(nameof(MpvPlayerControl.PlaybackState));
        Assert(stateProperty != null, "PlaybackState property 反射應存在");
        Assert(stateProperty!.GetSetMethod(nonPublic: false) == null, "PlaybackState 不應有 public setter");
    }

    /// <summary>
    /// 驗證 5 個 Commands 全暴露且無 player 時 CanExecute=false。
    /// </summary>
    private static void VerifyCommandsExposed()
    {
        using MpvPlayerControl control = new MpvPlayerControl();
        ICommand[] commands = new[]
        {
            control.PlayCommand, control.PauseCommand, control.StopCommand,
            control.TogglePauseCommand, control.ToggleMuteCommand
        };
        for (int index = 0; index < commands.Length; index++)
        {
            Assert(commands[index] != null, "Command index=" + index + " 不可為 null");
            Assert(commands[index].CanExecute(null) == false,
                "Command index=" + index + " 無 player 時 CanExecute 應為 false");
        }
    }

    /// <summary>
    /// 驗證無 player 時 Commands.Execute 安全（守備路徑）。
    /// </summary>
    private static void VerifyCommandsSafeWithoutPlayer()
    {
        using MpvPlayerControl control = new MpvPlayerControl();
        control.PlayCommand.Execute(null);
        control.PauseCommand.Execute(null);
        control.StopCommand.Execute(null);
        control.TogglePauseCommand.Execute(null);
        control.ToggleMuteCommand.Execute(null);
        // 抵達此處表示守備路徑正確。
    }

    /// <summary>
    /// 條件斷言；失敗時擲出含訊息的例外。
    /// </summary>
    /// <param name="condition">應為 true 的條件。</param>
    /// <param name="message">失敗訊息。</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
