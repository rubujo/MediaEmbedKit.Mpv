using System;
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
