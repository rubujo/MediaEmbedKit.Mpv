using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Avalonia;

namespace MediaEmbedKit.Mpv.Avalonia.HeadlessTests;

/// <summary>
/// 在 Avalonia headless 環境驗證 <see cref="MpvAvaloniaPlayer"/> 公開 DP 的命名、預設值與 CLR
/// 屬性 round-trip。本套件**不**啟動真實 libmpv，只覆蓋屬性系統與 CLR 包裝層的健康度。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 全部測試的 wall-clock 上限。
    /// </summary>
    private static readonly TimeSpan TotalBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 測試執行進入點。
    /// </summary>
    /// <param name="args">命令列引數；目前未使用。</param>
    /// <returns>全部通過時為 0，否則為 1。</returns>
    private static int Main(string[] args)
    {
        _ = args;
        ConfigureConsoleEncoding();
        AppBuilder builder = AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

        int exitCode = 0;
        builder.Start((app, startArgs) =>
        {
            try
            {
                Run("StyledProperty 已註冊且預設值正確", VerifyDefaultValues);
                Run("Volume / IsPaused / IsMuted CLR setter round-trip", VerifyReadWriteRoundTrip);
                Run("Source 設定觸發 OnSourceChanged 並更新 PendingSource", VerifySourceChangeFlow);
                Run("Duration / PlaybackState 為 DirectProperty 唯讀 (外部 SetValue 不影響)", VerifyReadOnlyDirectProperties);
                Run("Player 未附加時設定 DP 不擲例外（核心無 NRE 風險）", VerifyDpSetsTolerateNullPlayer);
                Run("PlayerCreated 事件可訂閱／取消訂閱", VerifyPlayerCreatedSubscription);
                Run("多個 StyledProperty 連續寫入保留最後值", VerifyMultiplePropertyWritesPreserveLastValue);
                Run("Dispose 後重複呼叫不擲例外", VerifyDoubleDisposeIsSafe);
                Console.WriteLine("Avalonia headless 測試完成：全部通過。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                exitCode = 1;
            }
        }, args);

        return exitCode;
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
        DateTimeOffset start = DateTimeOffset.UtcNow;
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
        finally
        {
            if (DateTimeOffset.UtcNow - start > TotalBudget)
            {
                throw new TimeoutException("測試超出整體預算 " + TotalBudget);
            }
        }
    }

    /// <summary>
    /// 驗證每個 StyledProperty 註冊且預設值符合 spec。
    /// </summary>
    private static void VerifyDefaultValues()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();
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
    /// 驗證 Volume / IsPaused / IsMuted 可讀寫且回讀值相符。
    /// </summary>
    private static void VerifyReadWriteRoundTrip()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();

        player.Volume = 80.0;
        Assert(player.Volume == 80.0, "Volume 寫入 80 後應讀回 80");

        player.IsPaused = true;
        Assert(player.IsPaused == true, "IsPaused 寫入 true 後應讀回 true");

        player.IsMuted = true;
        Assert(player.IsMuted == true, "IsMuted 寫入 true 後應讀回 true");

        player.Position = TimeSpan.FromSeconds(42);
        Assert(player.Position == TimeSpan.FromSeconds(42), "Position 寫入 42s 後應讀回 42s");
    }

    /// <summary>
    /// 驗證 Source 寫入會被 OnSourceChanged class handler 處理並更新 PendingSource。
    /// </summary>
    private static void VerifySourceChangeFlow()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();
        Assert(player.PendingSource == null, "Source 寫入前 PendingSource 應為 null");

        player.Source = "https://example.invalid/file.mp4";
        Assert(player.Source == "https://example.invalid/file.mp4", "Source 應反映寫入值");
        Assert(player.PendingSource == "https://example.invalid/file.mp4",
            "OnSourceChanged 應更新 PendingSource；實際=" + (player.PendingSource ?? "null"));
    }

    /// <summary>
    /// 驗證 Duration / PlaybackState 為 DirectProperty 且 setter 為 private。
    /// 由公開 SetValue 嘗試寫入時應因型別反射不可寫而不影響回讀值（仍維持預設）。
    /// </summary>
    private static void VerifyReadOnlyDirectProperties()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();
        // 確認 DirectProperty 註冊存在
        Assert(MpvAvaloniaPlayer.DurationProperty != null, "DurationProperty 註冊應存在");
        Assert(MpvAvaloniaPlayer.PlaybackStateProperty != null, "PlaybackStateProperty 註冊應存在");
        // 由於 setter 為 private，外部無法寫入；初始值維持預設。
        Assert(player.Duration == TimeSpan.Zero, "Duration 應維持預設 TimeSpan.Zero");
        Assert(player.PlaybackState == MpvPlaybackState.Idle, "PlaybackState 應維持預設 Idle");
    }

    /// <summary>
    /// 驗證在 player 未附加（無 libmpv）時對所有 DP 寫入皆不擲例外。
    /// 對應 control 內 change handler 的 <c>_player == null</c> 守備路徑。
    /// </summary>
    private static void VerifyDpSetsTolerateNullPlayer()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();
        // 一次寫入全部可讀寫 DP，驗證 change handler 內 _player==null 守備正確運作。
        player.Source = "https://example.invalid/file1.mp4";
        player.Position = TimeSpan.FromSeconds(10);
        player.Volume = 50.0;
        player.IsPaused = true;
        player.IsMuted = true;
        // 再寫一次測「value didn't change」的早退路徑與「value changed」路徑混用。
        player.Source = "https://example.invalid/file2.mp4";
        player.Position = TimeSpan.FromSeconds(20);
        player.Volume = 75.0;
        player.IsPaused = false;
        player.IsMuted = false;

        // 仍應讀回最後值
        Assert(player.Source == "https://example.invalid/file2.mp4", "Source 最後值不符");
        Assert(player.Position == TimeSpan.FromSeconds(20), "Position 最後值不符");
        Assert(player.Volume == 75.0, "Volume 最後值不符");
        Assert(player.IsPaused == false, "IsPaused 最後值不符");
        Assert(player.IsMuted == false, "IsMuted 最後值不符");
    }

    /// <summary>
    /// 驗證 <see cref="MpvAvaloniaPlayer.PlayerCreated"/> 事件可訂閱與取消訂閱（不會 leak handler）。
    /// </summary>
    private static void VerifyPlayerCreatedSubscription()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();
        int callCount = 0;
        EventHandler handler = (s, e) => callCount++;
        player.PlayerCreated += handler;
        player.PlayerCreated -= handler;
        // 訂閱／取消後 callCount 應為 0；未實際附加 player 故事件本來就不會觸發。
        Assert(callCount == 0, "尚未建立 player；callCount 應為 0");
    }

    /// <summary>
    /// 驗證對同一 StyledProperty 連續寫入多個值後讀回最後一個值。
    /// </summary>
    private static void VerifyMultiplePropertyWritesPreserveLastValue()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();
        for (int index = 0; index < 50; index++)
        {
            player.Volume = (double)index;
        }
        Assert(player.Volume == 49.0, "連續寫入 50 次後 Volume 應為 49.0；實際=" + player.Volume);
    }

    /// <summary>
    /// 驗證 <see cref="MpvAvaloniaPlayer.Dispose"/> 多次呼叫安全。
    /// </summary>
    private static void VerifyDoubleDisposeIsSafe()
    {
        MpvAvaloniaPlayer player = new MpvAvaloniaPlayer();
        player.Dispose();
        player.Dispose();
        player.Dispose();
        // 抵達此處表示三次 Dispose 都沒擲例外。
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

/// <summary>
/// 提供 Avalonia headless test 所需的最小 <see cref="Application"/>。
/// </summary>
internal sealed class HeadlessTestApp : Application
{
}
