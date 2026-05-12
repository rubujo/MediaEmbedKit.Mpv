using System;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.ConsoleMinimal
{
    /// <summary>
    /// 提供核心播放器最小生命週期範例的進入點。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 執行 Console minimal sample。
        /// </summary>
        /// <param name="args">第一個引數可指定要播放的檔案路徑或媒體網址。</param>
        /// <returns>處理程序結束代碼。</returns>
        private static async Task<int> Main(string[] args)
        {
            string source = args.Length > 0 ? args[0] : SampleRuntime.PlaybackUrl;
            try
            {
                Console.WriteLine("準備 Windows x64 runtime...");
                await SampleRuntime.InstallOrUpdateAsync().ConfigureAwait(false);

                MpvPlayerOptions options = new MpvPlayerOptions();
                SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, options);

                using MpvPlayer player = new MpvPlayer(options);
                player.EventReceived += PlayerEventReceived;
                player.LogMessageReceived += PlayerLogMessageReceived;
                player.FileLoaded += PlayerFileLoaded;
                player.Shutdown += PlayerShutdown;

                player.Initialize();
                Console.WriteLine("載入媒體：" + source);
                player.LoadFile(source, MpvLoadFileMode.Replace);

                await SampleRuntime.WaitForPlaybackAsync("ConsoleMinimalSample", () => player).ConfigureAwait(false);
                Console.WriteLine("播放已開始，按 Enter 停止。");
                if (Console.IsInputRedirected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                else
                {
                    Console.ReadLine();
                }

                player.Stop();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        /// <summary>
        /// 輸出 libmpv 一般事件。
        /// </summary>
        /// <param name="sender">引發事件的播放器。</param>
        /// <param name="e">libmpv 事件資料。</param>
        private static void PlayerEventReceived(object? sender, MpvEventArgs e)
        {
            Console.WriteLine("[event] " + e.EventId + " error=" + e.ErrorCode + " reply=" + e.ReplyUserData);
        }

        /// <summary>
        /// 輸出 libmpv 記錄訊息。
        /// </summary>
        /// <param name="sender">引發事件的播放器。</param>
        /// <param name="e">libmpv 記錄訊息資料。</param>
        private static void PlayerLogMessageReceived(object? sender, MpvLogMessageEventArgs e)
        {
            Console.WriteLine("[log] " + e.Level + " " + e.Prefix + " | " + e.Text.TrimEnd());
        }

        /// <summary>
        /// 輸出檔案載入完成事件。
        /// </summary>
        /// <param name="sender">引發事件的播放器。</param>
        /// <param name="e">libmpv 事件資料。</param>
        private static void PlayerFileLoaded(object? sender, MpvEventArgs e)
        {
            Console.WriteLine("[lifecycle] file-loaded");
        }

        /// <summary>
        /// 輸出播放器關閉事件。
        /// </summary>
        /// <param name="sender">引發事件的播放器。</param>
        /// <param name="e">libmpv 事件資料。</param>
        private static void PlayerShutdown(object? sender, MpvEventArgs e)
        {
            Console.WriteLine("[lifecycle] shutdown");
        }
    }
}
