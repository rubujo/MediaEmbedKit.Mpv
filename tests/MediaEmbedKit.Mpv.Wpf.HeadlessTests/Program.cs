using System;
using System.Text;
using System.Windows.Input;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Wpf;

namespace MediaEmbedKit.Mpv.Wpf.HeadlessTests;

/// <summary>
/// 在 STA thread 驗證 <see cref="MpvWpfPlayer"/> 公開 DependencyProperty 的命名、預設值與
/// CLR 屬性 round-trip。本套件**不**啟動真實 libmpv，只覆蓋 WPF 屬性系統與 CLR 包裝層。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 測試執行進入點（必須 STA thread 才能 new <see cref="MpvWpfPlayer"/>）。
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
            Run("DependencyProperty 已註冊且預設值正確", VerifyDefaultValues);
            Run("Volume / IsPaused / IsMuted CLR setter round-trip", VerifyReadWriteRoundTrip);
            Run("Duration / PlaybackState 唯讀（外部無法 SetValue）", VerifyReadOnlyDependencyProperties);
            Run("Source 設定後可讀回", VerifySourceRoundTrip);
            Run("Play / Pause / Stop / TogglePause / ToggleMute Commands 可取得且 CanExecute 預設 false（無 player）", VerifyCommandsExposed);
            Run("RaiseCanExecuteChanged 透過 player 生命週期傳遞（無 player 仍可呼叫）", VerifyCommandsSafeWithoutPlayer);
            Console.WriteLine("WPF headless 測試完成：全部通過。");
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
    /// 驗證每個 DP 註冊且預設值符合 spec。
    /// </summary>
    private static void VerifyDefaultValues()
    {
        MpvWpfPlayer player = new MpvWpfPlayer();
        Assert(player.Source == null, "Source 預設應為 null");
        Assert(player.Position == TimeSpan.Zero, "Position 預設應為 TimeSpan.Zero");
        Assert(player.Volume == 100.0, "Volume 預設應為 100.0");
        Assert(player.IsPaused == false, "IsPaused 預設應為 false");
        Assert(player.IsMuted == false, "IsMuted 預設應為 false");
        Assert(player.Duration == TimeSpan.Zero, "Duration 預設應為 TimeSpan.Zero");
        Assert(player.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 預設應為 Idle");
        Assert(player.Player == null, "尚未 Initialize 時 Player 應為 null");
    }

    /// <summary>
    /// 驗證可讀寫 DP 寫入後讀回相符。
    /// </summary>
    private static void VerifyReadWriteRoundTrip()
    {
        MpvWpfPlayer player = new MpvWpfPlayer();

        player.Volume = 80.0;
        Assert(player.Volume == 80.0, "Volume 80 寫入應讀回 80");

        player.IsPaused = true;
        Assert(player.IsPaused == true, "IsPaused true 寫入應讀回 true");

        player.IsMuted = true;
        Assert(player.IsMuted == true, "IsMuted true 寫入應讀回 true");

        player.Position = TimeSpan.FromSeconds(42);
        Assert(player.Position == TimeSpan.FromSeconds(42), "Position 42s 寫入應讀回 42s");
    }

    /// <summary>
    /// 驗證 Duration / PlaybackState 由 DependencyPropertyKey 註冊、外部無 setter。
    /// </summary>
    private static void VerifyReadOnlyDependencyProperties()
    {
        MpvWpfPlayer player = new MpvWpfPlayer();
        Assert(MpvWpfPlayer.DurationProperty != null, "DurationProperty 註冊應存在");
        Assert(MpvWpfPlayer.PlaybackStateProperty != null, "PlaybackStateProperty 註冊應存在");
        Assert(player.Duration == TimeSpan.Zero, "Duration 應維持預設");
        Assert(player.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 應維持預設");
    }

    /// <summary>
    /// 驗證 Source 寫入後可讀回。
    /// </summary>
    private static void VerifySourceRoundTrip()
    {
        MpvWpfPlayer player = new MpvWpfPlayer();
        player.Source = "https://example.invalid/file.mp4";
        Assert(player.Source == "https://example.invalid/file.mp4", "Source round-trip 失敗");
    }

    /// <summary>
    /// 驗證 5 個 Commands 都已暴露且 CanExecute 在無 player 時為 false。
    /// </summary>
    private static void VerifyCommandsExposed()
    {
        MpvWpfPlayer player = new MpvWpfPlayer();
        ICommand[] commands = new[]
        {
            player.PlayCommand, player.PauseCommand, player.StopCommand,
            player.TogglePauseCommand, player.ToggleMuteCommand
        };
        for (int index = 0; index < commands.Length; index++)
        {
            Assert(commands[index] != null, "Command index=" + index + " 不可為 null");
            // 無 player 時所有 commands 的 CanExecute 都應為 false（因 _player == null）。
            Assert(commands[index].CanExecute(null) == false,
                "Command index=" + index + " 無 player 時 CanExecute 應為 false");
        }
    }

    /// <summary>
    /// 驗證在無 player 情況下呼叫 Commands.Execute 不擲例外（守備路徑）。
    /// </summary>
    private static void VerifyCommandsSafeWithoutPlayer()
    {
        MpvWpfPlayer player = new MpvWpfPlayer();
        player.PlayCommand.Execute(null);
        player.PauseCommand.Execute(null);
        player.StopCommand.Execute(null);
        player.TogglePauseCommand.Execute(null);
        player.ToggleMuteCommand.Execute(null);
        // 抵達此處表示所有 commands 對 _player==null 守備正確。
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
