using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.Samples;

/// <summary>
/// 提供範例 UI 共用的進階功能展示動作。
/// </summary>
internal sealed class SampleFeatureController
{
    /// <summary>
    /// 外部工具診斷輸出的最大列數。
    /// </summary>
    private const int DiagnosticOutputLineLimit = 40;
    /// <summary>
    /// 取得目前播放器執行個體的委派。
    /// </summary>
    private readonly Func<MpvPlayer?> _getPlayer;
    /// <summary>
    /// 將診斷訊息送回 UI 的委派。
    /// </summary>
    private readonly Action<string> _appendLine;
    /// <summary>
    /// 目前使用的播放速度。
    /// </summary>
    private double _currentSpeed = 1.0;
    /// <summary>
    /// 紀錄範例 Lua 指令碼是否已經要求載入。
    /// </summary>
    private int _sampleLuaScriptLoaded;

    /// <summary>
    /// 初始化 <see cref="SampleFeatureController"/> 類別的新執行個體。
    /// </summary>
    /// <param name="getPlayer">取得目前播放器的委派。</param>
    /// <param name="appendLine">接收診斷訊息的委派。</param>
    public SampleFeatureController(Func<MpvPlayer?> getPlayer, Action<string> appendLine)
    {
        _getPlayer = getPlayer ?? throw new ArgumentNullException(nameof(getPlayer));
        _appendLine = appendLine ?? throw new ArgumentNullException(nameof(appendLine));
    }

    /// <summary>
    /// 建立範例預設的 yt-dlp 格式選項清單。
    /// </summary>
    /// <returns>可繫結到 UI 下拉選單的格式選項清單。</returns>
    public static IReadOnlyList<SampleYtdlpFormatChoice> CreateYtdlpFormatChoices()
    {
        return new[]
        {
            new SampleYtdlpFormatChoice("流暢 720p", SampleRuntime.SmoothPlaybackYtdlpFormat),
            new SampleYtdlpFormatChoice("預設", MpvYtdlpFormatPreset.Default),
            new SampleYtdlpFormatChoice("最佳", MpvYtdlpFormatPreset.Best),
            new SampleYtdlpFormatChoice("最高 1080p", MpvYtdlpFormatPreset.UpTo1080p),
            new SampleYtdlpFormatChoice("最高 720p", MpvYtdlpFormatPreset.UpTo720p),
            new SampleYtdlpFormatChoice("最高 480p", MpvYtdlpFormatPreset.UpTo480p),
            new SampleYtdlpFormatChoice("只有音訊", MpvYtdlpFormatPreset.AudioOnly)
        };
    }

    /// <summary>
    /// 取得範例預設的 yt-dlp 格式選項。
    /// </summary>
    /// <returns>以播放順暢度優先的 720p 格式選項。</returns>
    public static SampleYtdlpFormatChoice CreateDefaultYtdlpFormatChoice()
    {
        return new SampleYtdlpFormatChoice("流暢 720p", SampleRuntime.SmoothPlaybackYtdlpFormat);
    }

    /// <summary>
    /// 將 yt-dlp 格式選項套用到播放器選項。
    /// </summary>
    /// <param name="options">要套用的播放器選項。</param>
    /// <param name="choice">要套用的格式選項。</param>
    public static void ApplyYtdlpFormat(MpvPlayerOptions options, SampleYtdlpFormatChoice choice)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (choice == null)
        {
            throw new ArgumentNullException(nameof(choice));
        }

        options.YtdlpFormatPreset = choice.Preset;
        options.YtdlpFormat = choice.UsesPreset ? null : choice.Selector;
        options.InitialOptions["ytdl-format"] = choice.Selector;
    }

    /// <summary>
    /// 將 yt-dlp 格式選項套用到目前播放器。
    /// </summary>
    /// <param name="choice">要套用的格式選項。</param>
    public void ApplyYtdlpFormat(SampleYtdlpFormatChoice choice)
    {
        MpvPlayer player = RequirePlayer();
        player.SetYtdlpFormat(choice.Selector);
        Append("yt-dlp", "已套用格式：" + choice.DisplayName + " => " + choice.Selector + "。重新載入網址後會使用新格式。");
        player.ShowText("yt-dlp format: " + choice.DisplayName, 1800, 1);
    }

    /// <summary>
    /// 顯示 OSD 文字。
    /// </summary>
    public void ShowOsd()
    {
        MpvPlayer player = RequirePlayer();
        player.ShowText("MediaEmbedKit.Mpv OSD 範例", 2200, 1);
        Append("api", "已呼叫 ShowText。");
    }

    /// <summary>
    /// 依指定秒數相對跳轉。
    /// </summary>
    /// <param name="seconds">要相對跳轉的秒數。</param>
    public void SeekRelative(double seconds)
    {
        MpvPlayer player = RequirePlayer();
        player.Seek(seconds);
        Append("api", "已呼叫 Seek(" + seconds.ToString("0.###", CultureInfo.InvariantCulture) + ")。");
    }

    /// <summary>
    /// 依指定差值調整音量。
    /// </summary>
    /// <param name="delta">音量差值。</param>
    public void ChangeVolume(double delta)
    {
        MpvPlayer player = RequirePlayer();
        double nextVolume = Math.Max(0, Math.Min(130, player.Volume + delta));
        player.Volume = nextVolume;
        Append("api", "Volume = " + nextVolume.ToString("0.#", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 切換靜音狀態。
    /// </summary>
    public void ToggleMute()
    {
        MpvPlayer player = RequirePlayer();
        player.Mute = !player.Mute;
        Append("api", "Mute = " + player.Mute);
    }

    /// <summary>
    /// 在常用播放速度之間切換。
    /// </summary>
    public void CycleSpeed()
    {
        MpvPlayer player = RequirePlayer();
        if (_currentSpeed < 1.25)
        {
            _currentSpeed = 1.25;
        }
        else if (_currentSpeed < 1.5)
        {
            _currentSpeed = 1.5;
        }
        else
        {
            _currentSpeed = 1.0;
        }

        player.Speed = _currentSpeed;
        Append("api", "Speed = " + _currentSpeed.ToString("0.##", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 載入範例本機字幕。
    /// </summary>
    public void AddSampleSubtitle()
    {
        SampleRuntime.EnsureSampleFiles();
        MpvPlayer player = RequirePlayer();
        player.AddSubtitle(SampleRuntime.SampleSubtitlePath, MpvTrackLoadMode.Select, MpvTrackLoadFlags.Default, "範例字幕", "zh-TW");
        Append("api", "已載入外部字幕：" + SampleRuntime.SampleSubtitlePath);
    }

    /// <summary>
    /// 將目前播放軌清單輸出到事件清單。
    /// </summary>
    public void DumpTracks()
    {
        MpvPlayer player = RequirePlayer();
        IReadOnlyList<MpvTrackInfo> tracks = player.GetTracks();
        Append("tracks", "播放軌數量：" + tracks.Count.ToString(CultureInfo.InvariantCulture));
        foreach (MpvTrackInfo track in tracks)
        {
            Append("tracks", "#" + track.Id.ToString(CultureInfo.InvariantCulture) + " " + track.Type + " codec=" + (track.Codec ?? "null") + " selected=" + track.Selected);
        }
    }

    /// <summary>
    /// 將目前畫面截圖到範例截圖資料夾。
    /// </summary>
    public void TakeScreenshot()
    {
        Directory.CreateDirectory(SampleRuntime.ScreenshotDirectory);
        string fileName = Path.Combine(
            SampleRuntime.ScreenshotDirectory,
            "sample-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
        MpvPlayer player = RequirePlayer();
        string actualPath = player.TakeScreenshotToFile(fileName, MpvScreenshotMode.Video);
        Append("api", "已輸出截圖：" + actualPath);
    }

    /// <summary>
    /// 載入範例 mpv 設定檔。
    /// </summary>
    public void LoadSampleConfig()
    {
        SampleRuntime.EnsureSampleFiles();
        MpvPlayer player = RequirePlayer();
        player.LoadConfigFile(SampleRuntime.SampleConfigFilePath);
        player.ShowText("已載入範例 mpv.conf", 2000, 1);
        Append("api", "已載入設定檔：" + SampleRuntime.SampleConfigFilePath);
    }

    /// <summary>
    /// 載入範例 Lua 指令碼並確認指令碼可接收訊息。
    /// </summary>
    /// <returns>代表 Lua 指令碼載入與確認流程的工作。</returns>
    public async Task LoadSampleLuaScriptAsync()
    {
        SampleRuntime.EnsureSampleFiles();
        MpvPlayer player = RequirePlayer();
        TaskCompletionSource<MpvClientMessageEventArgs> reply =
            new TaskCompletionSource<MpvClientMessageEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<MpvClientMessageEventArgs> handler = delegate (object? sender, MpvClientMessageEventArgs e)
        {
            if (e.Arguments.Count > 0 && string.Equals(e.Arguments[0], "sample-pong", StringComparison.Ordinal))
            {
                reply.TrySetResult(e);
            }
        };

        player.ClientMessage += handler;
        try
        {
            if (Interlocked.CompareExchange(ref _sampleLuaScriptLoaded, 1, 0) == 0)
            {
                try
                {
                    player.LoadScript(SampleRuntime.SampleLuaScriptPath);
                    Append("api", "已載入 Lua 指令碼：" + SampleRuntime.SampleLuaScriptPath);
                }
                catch
                {
                    Volatile.Write(ref _sampleLuaScriptLoaded, 0);
                    throw;
                }
            }

            MpvClientMessageEventArgs message = await WaitForLuaScriptReplyAsync(player, reply.Task).ConfigureAwait(false);
            string detail = message.Arguments.Count > 1 ? message.Arguments[1] : "sample-pong";
            Append("api", "Lua 指令碼已回覆：" + detail);
        }
        finally
        {
            player.ClientMessage -= handler;
        }
    }

    /// <summary>
    /// 等待範例 Lua 指令碼回覆。
    /// </summary>
    /// <param name="player">要送出指令碼訊息的播放器。</param>
    /// <param name="replyTask">等待指令碼回覆的工作。</param>
    /// <returns>代表 Lua 指令碼回覆的事件資料。</returns>
    private static async Task<MpvClientMessageEventArgs> WaitForLuaScriptReplyAsync(MpvPlayer player, Task<MpvClientMessageEventArgs> replyTask)
    {
        using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
        {
            while (!replyTask.IsCompleted && !timeout.IsCancellationRequested)
            {
                player.SendScriptMessage("sample-ping", "Lua script 已載入");
                Task delayTask = Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
                Task completedTask = await Task.WhenAny(replyTask, delayTask).ConfigureAwait(false);
                if (completedTask == replyTask)
                {
                    return await replyTask.ConfigureAwait(false);
                }
            }
        }

        throw new TimeoutException("等待 Lua 指令碼回覆逾時。");
    }

    /// <summary>
    /// 執行 yt-dlp 診斷命令並輸出版本與格式清單摘要。
    /// </summary>
    /// <param name="url">要交給 yt-dlp 檢查的媒體網址。</param>
    /// <returns>代表診斷流程的工作。</returns>
    public async Task RunYtdlpDiagnosticsAsync(string url)
    {
        Append("yt-dlp", "版本：" + (YtDlpDownloader.GetInstalledVersion(SampleRuntime.YtDlpPath) ?? "無法讀取"));
        YtDlpProcessRunner runner = new YtDlpProcessRunner(SampleRuntime.YtDlpPath)
        {
            WorkingDirectory = SampleRuntime.RuntimeDirectory
        };
        EventHandler<ExternalToolOutputEventArgs> handler = AttachExternalToolOutput(runner, "yt-dlp");
        ExternalToolProcessResult result;
        try
        {
            result = await runner.ListFormatsAsync(
                url,
                TimeSpan.FromSeconds(90),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            runner.OutputReceived -= handler;
        }

        AppendProcessResult("yt-dlp", result);
    }

    /// <summary>
    /// 執行 Deno 診斷命令並輸出版本。
    /// </summary>
    /// <returns>代表診斷流程的工作。</returns>
    public async Task RunDenoDiagnosticsAsync()
    {
        Append("deno", "版本：" + (DenoDownloader.GetInstalledVersion(SampleRuntime.DenoPath) ?? "無法讀取"));
        DenoProcessRunner runner = new DenoProcessRunner(SampleRuntime.DenoPath)
        {
            WorkingDirectory = SampleRuntime.RuntimeDirectory
        };
        EventHandler<ExternalToolOutputEventArgs> handler = AttachExternalToolOutput(runner, "deno");
        ExternalToolProcessResult result;
        try
        {
            result = await runner.GetVersionAsync(
                TimeSpan.FromSeconds(30),
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            runner.OutputReceived -= handler;
        }

        AppendProcessResult("deno", result);
    }

    /// <summary>
    /// 執行 FFmpeg 與 FFprobe 診斷命令並輸出版本資訊。
    /// </summary>
    /// <returns>代表診斷流程的工作。</returns>
    public async Task RunFFmpegDiagnosticsAsync()
    {
        Append("ffmpeg", "版本：" + (FFmpegDownloader.GetInstalledVersion(SampleRuntime.FFmpegPath) ?? "無法讀取"));
        Append("ffprobe", "版本：" + (FFmpegDownloader.GetInstalledVersion(SampleRuntime.FFprobePath) ?? "無法讀取"));

        await RunExternalToolAsync("ffmpeg", SampleRuntime.FFmpegPath, new[] { "-hide_banner", "-version" }).ConfigureAwait(false);
        await RunExternalToolAsync("ffprobe", SampleRuntime.FFprobePath, new[] { "-hide_banner", "-version" }).ConfigureAwait(false);
    }

    /// <summary>
    /// 以共用流程執行外部工具並輸出結果。
    /// </summary>
    /// <param name="category">事件分類名稱。</param>
    /// <param name="executablePath">外部工具可執行檔路徑。</param>
    /// <param name="arguments">要傳給外部工具的引數集合。</param>
    /// <returns>代表執行流程的工作。</returns>
    private async Task RunExternalToolAsync(string category, string executablePath, IReadOnlyList<string> arguments)
    {
        ExternalToolProcessRunner runner = new ExternalToolProcessRunner(executablePath)
        {
            WorkingDirectory = SampleRuntime.RuntimeDirectory
        };
        int emittedLines = 0;
        EventHandler<ExternalToolOutputEventArgs> handler = delegate (object? sender, ExternalToolOutputEventArgs e)
        {
            int lineNumber = Interlocked.Increment(ref emittedLines);
            AppendExternalToolOutput(category, e, lineNumber);
        };
        runner.OutputReceived += handler;
        ExternalToolProcessResult result;
        try
        {
            result = await runner.RunAsync(arguments, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            runner.OutputReceived -= handler;
        }

        AppendProcessResult(category, result);
    }

    /// <summary>
    /// 執行 yt-dlp 自我更新命令。
    /// </summary>
    /// <returns>代表更新流程的工作。</returns>
    public async Task RunYtdlpSelfUpdateAsync()
    {
        ToolUpdateResult result = await YtDlpDownloader.RunSelfUpdateAsync(SampleRuntime.YtDlpPath).ConfigureAwait(false);
        AppendProcessResult("yt-dlp-update", result);
    }

    /// <summary>
    /// 執行 Deno 自我更新命令。
    /// </summary>
    /// <returns>代表更新流程的工作。</returns>
    public async Task RunDenoSelfUpgradeAsync()
    {
        ToolUpdateResult result = await DenoDownloader.RunSelfUpgradeAsync(SampleRuntime.DenoPath).ConfigureAwait(false);
        AppendProcessResult("deno-upgrade", result);
    }

    /// <summary>
    /// 取得目前播放器狀態摘要。
    /// </summary>
    /// <returns>可顯示在狀態列的播放器狀態文字。</returns>
    public string GetStatusText()
    {
        MpvPlayer? player = _getPlayer();
        if (player == null || !player.IsInitialized)
        {
            return "播放器尚未初始化";
        }

        string timePosition = TryGetDouble(player, "time-pos");
        string duration = TryGetDouble(player, "duration");
        string volume = TryGetDouble(player, "volume");
        string speed = TryGetDouble(player, "speed");
        string mute = TryGetFlag(player, "mute");
        return "time " + timePosition + " / " + duration + " | vol " + volume + " | mute " + mute + " | speed " + speed;
    }

    /// <summary>
    /// 取得目前播放器，若尚未建立則擲回例外狀況。
    /// </summary>
    /// <returns>目前播放器。</returns>
    private MpvPlayer RequirePlayer()
    {
        MpvPlayer? player = _getPlayer();
        if (player == null || !player.IsInitialized)
        {
            throw new InvalidOperationException("播放器尚未初始化。");
        }

        return player;
    }

    /// <summary>
    /// 將訊息送回事件清單。
    /// </summary>
    /// <param name="category">訊息分類。</param>
    /// <param name="message">訊息內容。</param>
    private void Append(string category, string message)
    {
        string line = DateTimeOffset.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [" + category + "] " + message;
        _appendLine(line);
    }

    /// <summary>
    /// 讀取 double 屬性並格式化。
    /// </summary>
    /// <param name="player">要讀取的播放器。</param>
    /// <param name="name">屬性名稱。</param>
    /// <returns>格式化後的屬性值。</returns>
    private static string TryGetDouble(MpvPlayer player, string name)
    {
        try
        {
            return player.GetPropertyDouble(name).ToString("0.##", CultureInfo.InvariantCulture);
        }
        catch (MpvException)
        {
            return "-";
        }
    }

    /// <summary>
    /// 讀取布林屬性並格式化。
    /// </summary>
    /// <param name="player">要讀取的播放器。</param>
    /// <param name="name">屬性名稱。</param>
    /// <returns>格式化後的屬性值。</returns>
    private static string TryGetFlag(MpvPlayer player, string name)
    {
        try
        {
            return player.GetPropertyFlag(name).ToString(CultureInfo.InvariantCulture);
        }
        catch (MpvException)
        {
            return "-";
        }
    }

    /// <summary>
    /// 將外部工具執行結果輸出到事件清單。
    /// </summary>
    /// <param name="category">訊息分類。</param>
    /// <param name="result">工具執行結果。</param>
    private void AppendProcessResult(string category, ToolUpdateResult result)
    {
        Append(category, Path.GetFileName(result.ExecutablePath) + " " + result.Arguments + " exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
        AppendOutputLines(category + ":out", result.StandardOutput);
        AppendOutputLines(category + ":err", result.StandardError);
    }

    /// <summary>
    /// 將外部工具執行結果輸出到事件清單。
    /// </summary>
    /// <param name="category">訊息分類。</param>
    /// <param name="result">工具執行結果。</param>
    private void AppendProcessResult(string category, ExternalToolProcessResult result)
    {
        Append(category, Path.GetFileName(result.ExecutablePath) + " " + result.ArgumentText + " exit=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 將 yt-dlp 輸出事件連接到範例事件清單。
    /// </summary>
    /// <param name="runner">要觀察的 yt-dlp 處理序執行器。</param>
    /// <param name="category">訊息分類。</param>
    /// <returns>已註冊到輸出事件的處理常式。</returns>
    private EventHandler<ExternalToolOutputEventArgs> AttachExternalToolOutput(YtDlpProcessRunner runner, string category)
    {
        int emittedLines = 0;
        EventHandler<ExternalToolOutputEventArgs> handler = delegate (object? sender, ExternalToolOutputEventArgs e)
        {
            int lineNumber = Interlocked.Increment(ref emittedLines);
            AppendExternalToolOutput(category, e, lineNumber);
        };
        runner.OutputReceived += handler;
        return handler;
    }

    /// <summary>
    /// 將 Deno 輸出事件連接到範例事件清單。
    /// </summary>
    /// <param name="runner">要觀察的 Deno 處理序執行器。</param>
    /// <param name="category">訊息分類。</param>
    /// <returns>已註冊到輸出事件的處理常式。</returns>
    private EventHandler<ExternalToolOutputEventArgs> AttachExternalToolOutput(DenoProcessRunner runner, string category)
    {
        int emittedLines = 0;
        EventHandler<ExternalToolOutputEventArgs> handler = delegate (object? sender, ExternalToolOutputEventArgs e)
        {
            int lineNumber = Interlocked.Increment(ref emittedLines);
            AppendExternalToolOutput(category, e, lineNumber);
        };
        runner.OutputReceived += handler;
        return handler;
    }

    /// <summary>
    /// 將外部工具單列輸出寫入事件清單。
    /// </summary>
    /// <param name="category">訊息分類。</param>
    /// <param name="e">外部工具輸出事件資料。</param>
    /// <param name="lineNumber">目前已輸出的列數。</param>
    private void AppendExternalToolOutput(string category, ExternalToolOutputEventArgs e, int lineNumber)
    {
        if (lineNumber > DiagnosticOutputLineLimit)
        {
            return;
        }

        string streamName = e.Stream == ExternalToolOutputStream.StandardOutput ? "out" : "err";
        Append(category + ":" + streamName, e.Line);
    }

    /// <summary>
    /// 將外部工具輸出拆列並限制輸出數量。
    /// </summary>
    /// <param name="category">訊息分類。</param>
    /// <param name="text">外部工具輸出文字。</param>
    private void AppendOutputLines(string category, string text)
    {
        string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(DiagnosticOutputLineLimit)
            .ToArray();
        foreach (string line in lines)
        {
            Append(category, line);
        }
    }
}

/// <summary>
/// 表示範例 UI 可選擇的 yt-dlp 格式選項。
/// </summary>
internal sealed class SampleYtdlpFormatChoice
{
    /// <summary>
    /// 初始化 <see cref="SampleYtdlpFormatChoice"/> 類別的新執行個體。
    /// </summary>
    /// <param name="displayName">顯示在 UI 的名稱。</param>
    /// <param name="preset">對應的 yt-dlp 格式預設值。</param>
    public SampleYtdlpFormatChoice(string displayName, MpvYtdlpFormatPreset preset)
    {
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Preset = preset;
        Selector = MpvYtdlpFormatSelector.FromPreset(preset);
        UsesPreset = true;
    }

    /// <summary>
    /// 初始化 <see cref="SampleYtdlpFormatChoice"/> 類別的新執行個體。
    /// </summary>
    /// <param name="displayName">顯示在 UI 的名稱。</param>
    /// <param name="selector">對應的 yt-dlp selector 字串。</param>
    public SampleYtdlpFormatChoice(string displayName, string selector)
    {
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Preset = MpvYtdlpFormatPreset.Default;
        Selector = string.IsNullOrWhiteSpace(selector) ? throw new ArgumentException("yt-dlp selector 不可為空白。", nameof(selector)) : selector;
        UsesPreset = false;
    }

    /// <summary>
    /// 取得顯示在 UI 的名稱。
    /// </summary>
    /// <value>格式選項顯示名稱。</value>
    public string DisplayName { get; private set; }

    /// <summary>
    /// 取得對應的 yt-dlp 格式預設值。
    /// </summary>
    /// <value>yt-dlp 格式預設值。</value>
    public MpvYtdlpFormatPreset Preset { get; private set; }

    /// <summary>
    /// 取得對應的 yt-dlp selector 字串。
    /// </summary>
    /// <value>yt-dlp selector 字串。</value>
    public string Selector { get; private set; }

    /// <summary>
    /// 取得格式選項是否直接對應到內建預設值。
    /// </summary>
    /// <value>格式選項使用內建預設值時為 <see langword="true"/>。</value>
    public bool UsesPreset { get; private set; }

    /// <summary>
    /// 將格式選項轉成 UI 顯示文字。
    /// </summary>
    /// <returns>格式選項顯示名稱。</returns>
    public override string ToString()
    {
        return DisplayName;
    }
}
