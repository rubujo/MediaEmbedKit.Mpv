using System;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;

namespace MediaEmbedKit.Mpv.Samples;

/// <summary>
/// 提供 GUI 範例在視窗關閉前使用的播放器收尾輔助方法。
/// </summary>
internal static class SampleShutdown
{
    /// <summary>
    /// GUI 關閉流程可接受的 libmpv graceful shutdown 等待時間。
    /// </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// 在視窗真正釋放控制項前先要求 libmpv 結束，降低同步 Dispose 在 UI 執行緒等待事件執行緒的機率。
    /// </summary>
    /// <param name="player">
    /// 目前 sample 建立的播放器；尚未建立時可為 <see langword="null"/>。
    /// </param>
    /// <param name="writeLifecycle">
    /// 可選的生命週期事件輸出委派。
    /// </param>
    /// <returns>
    /// 代表關閉前收尾流程的工作。
    /// </returns>
    public static async Task PreparePlayerCloseAsync(MpvPlayer? player, Action<string, string>? writeLifecycle)
    {
        if (player == null || !player.IsInitialized)
        {
            return;
        }

        writeLifecycle?.Invoke("CloseRequested", "收到視窗關閉要求，準備停止 libmpv。");

        try
        {
            await player.ShutdownAsync(ShutdownTimeout).ConfigureAwait(false);
            writeLifecycle?.Invoke("CloseReady", "libmpv 已完成關閉前收尾。");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (MpvException ex)
        {
            writeLifecycle?.Invoke("CloseShutdownError", ex.Message);
        }
    }
}
