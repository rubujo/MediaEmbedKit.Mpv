using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MediaEmbedKit.Mpv.Externals;
using MediaEmbedKit.Mpv.Runtime;
using MediaEmbedKit.Mpv.Platforms;

namespace MediaEmbedKit.Mpv.Diagnostics;

/// <summary>
/// 提供執行階段資料夾的健康檢查工具。
/// 可在啟動或更新後判斷 libmpv 是否可載入、外部工具是否齊全。
/// </summary>
public static class MpvRuntimeHealthCheck
{
    /// <summary>
    /// libmpv 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string LibMpvFileName = "libmpv-2.dll";
    /// <summary>
    /// yt-dlp 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string YtdlpFileName = "yt-dlp.exe";
    /// <summary>
    /// Deno 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string DenoFileName = "deno.exe";
    /// <summary>
    /// FFmpeg 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string FFmpegFileName = "ffmpeg.exe";
    /// <summary>
    /// FFprobe 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string FFprobeFileName = "ffprobe.exe";

    /// <summary>
    /// 分析指定執行階段資料夾的健康狀態。
    /// </summary>
    /// <param name="runtimeDirectory">要分析的執行階段資料夾。</param>
    /// <param name="probeLibMpv">是否實際嘗試載入 libmpv 與建立 mpv handle。</param>
    /// <param name="cancellationToken">取消分析的 token。</param>
    /// <returns>分析結果。</returns>
    public static Task<MpvRuntimeHealthReport> AnalyzeAsync(
        string runtimeDirectory,
        bool probeLibMpv = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        string fullPath = Path.GetFullPath(runtimeDirectory);
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Analyze(fullPath, probeLibMpv);
            },
            cancellationToken);
    }

    /// <summary>
    /// 在當前執行緒同步分析指定執行階段資料夾的健康狀態。
    /// </summary>
    /// <param name="runtimeDirectory">已格式化為完整路徑的執行階段資料夾。</param>
    /// <param name="probeLibMpv">是否實際嘗試載入 libmpv 與建立 mpv handle。</param>
    /// <returns>分析結果。</returns>
    private static MpvRuntimeHealthReport Analyze(string runtimeDirectory, bool probeLibMpv)
    {
        List<string> errors = new List<string>();
        string libMpvPath = Path.Combine(runtimeDirectory, LibMpvFileName);
        bool libMpvPresent = File.Exists(libMpvPath);
        if (!libMpvPresent)
        {
            errors.Add("找不到 " + LibMpvFileName + "。");
        }

        bool libMpvLoadable = false;
        bool playerInitializable = false;
        string? clientApiVersion = null;

        if (libMpvPresent && probeLibMpv && !MpvLibraryLoader.IsLoaded)
        {
            try
            {
                MpvLibraryLoader.Load(libMpvPath);
                libMpvLoadable = true;
            }
            catch (Exception ex)
            {
                errors.Add("無法載入 libmpv：" + ex.Message);
            }

            if (libMpvLoadable)
            {
                try
                {
                    using (MpvPlayer player = new MpvPlayer(new MpvPlayerOptions { MpvLibraryPath = libMpvPath }))
                    {
                        player.Initialize();
                        MpvCapabilities capabilities = player.GetCapabilities();
                        clientApiVersion = capabilities.ClientApiVersion.ToString();
                        playerInitializable = true;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add("無法初始化 mpv player：" + ex.Message);
                }
            }
        }
        else if (libMpvPresent && probeLibMpv && MpvLibraryLoader.IsLoaded)
        {
            libMpvLoadable = true;
            clientApiVersion = ToVersionString(MpvPlayer.ClientApiVersion());
            playerInitializable = true;
        }

        bool ytdlpPresent = File.Exists(Path.Combine(runtimeDirectory, YtdlpFileName));
        bool denoPresent = File.Exists(Path.Combine(runtimeDirectory, DenoFileName));
        bool ffmpegPresent = File.Exists(Path.Combine(runtimeDirectory, FFmpegFileName));
        bool ffprobePresent = File.Exists(Path.Combine(runtimeDirectory, FFprobeFileName));

        return new MpvRuntimeHealthReport(
            runtimeDirectory,
            libMpvPresent,
            libMpvLoadable,
            playerInitializable,
            clientApiVersion,
            ytdlpPresent,
            denoPresent,
            ffmpegPresent,
            ffprobePresent,
            new ReadOnlyCollection<string>(errors));
    }

    /// <summary>
    /// 將 client API 版本整數轉成 major.minor 文字。
    /// </summary>
    /// <param name="rawVersion">由 libmpv 回傳的 client API 版本整數。</param>
    /// <returns>major.minor 文字。</returns>
    private static string ToVersionString(uint rawVersion)
    {
        int major = (int)((rawVersion >> 16) & 0xFFFF);
        int minor = (int)(rawVersion & 0xFFFF);
        return major.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + minor.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// 由 <see cref="MpvRuntimeHealthCheck"/> 產生的健康檢查報告。
/// </summary>
public sealed class MpvRuntimeHealthReport
{
    /// <summary>
    /// 初始化 <see cref="MpvRuntimeHealthReport"/> 類別的新執行個體。
    /// </summary>
    /// <param name="runtimeDirectory">被檢查的執行階段資料夾。</param>
    /// <param name="isLibMpvPresent">libmpv-2.dll 是否存在。</param>
    /// <param name="canLoadLibMpv">libmpv 是否可被當前處理序載入。</param>
    /// <param name="canInitializePlayer">是否能建立並初始化 mpv player。</param>
    /// <param name="clientApiVersion">已建立的 mpv player 回報的 client API 版本字串。</param>
    /// <param name="isYtdlpPresent">yt-dlp.exe 是否存在。</param>
    /// <param name="isDenoPresent">deno.exe 是否存在。</param>
    /// <param name="isFFmpegPresent">ffmpeg.exe 是否存在。</param>
    /// <param name="isFFprobePresent">ffprobe.exe 是否存在。</param>
    /// <param name="errors">分析過程蒐集到的錯誤訊息。</param>
    internal MpvRuntimeHealthReport(
        string runtimeDirectory,
        bool isLibMpvPresent,
        bool canLoadLibMpv,
        bool canInitializePlayer,
        string? clientApiVersion,
        bool isYtdlpPresent,
        bool isDenoPresent,
        bool isFFmpegPresent,
        bool isFFprobePresent,
        IReadOnlyList<string> errors)
    {
        RuntimeDirectory = runtimeDirectory;
        IsLibMpvPresent = isLibMpvPresent;
        CanLoadLibMpv = canLoadLibMpv;
        CanInitializePlayer = canInitializePlayer;
        ClientApiVersion = clientApiVersion;
        IsYtdlpPresent = isYtdlpPresent;
        IsDenoPresent = isDenoPresent;
        IsFFmpegPresent = isFFmpegPresent;
        IsFFprobePresent = isFFprobePresent;
        Errors = errors;
    }

    /// <summary>
    /// 取得被檢查的執行階段資料夾。
    /// </summary>
    /// <value>執行階段資料夾完整路徑。</value>
    public string RuntimeDirectory { get; }

    /// <summary>
    /// 取得 libmpv-2.dll 是否存在。
    /// </summary>
    /// <value>存在時為 <see langword="true"/>。</value>
    public bool IsLibMpvPresent { get; }

    /// <summary>
    /// 取得 libmpv 是否可被當前處理序載入。
    /// </summary>
    /// <value>可載入時為 <see langword="true"/>。</value>
    public bool CanLoadLibMpv { get; }

    /// <summary>
    /// 取得是否能建立並初始化 mpv player。
    /// </summary>
    /// <value>可成功初始化時為 <see langword="true"/>。</value>
    public bool CanInitializePlayer { get; }

    /// <summary>
    /// 取得已建立的 mpv player 回報的 client API 版本字串。
    /// </summary>
    /// <value>例如 <c>2.5</c>；未測試或失敗時為 <see langword="null"/>。</value>
    public string? ClientApiVersion { get; }

    /// <summary>
    /// 取得 yt-dlp.exe 是否存在。
    /// </summary>
    /// <value>存在時為 <see langword="true"/>。</value>
    public bool IsYtdlpPresent { get; }

    /// <summary>
    /// 取得 deno.exe 是否存在。
    /// </summary>
    /// <value>存在時為 <see langword="true"/>。</value>
    public bool IsDenoPresent { get; }

    /// <summary>
    /// 取得 ffmpeg.exe 是否存在。
    /// </summary>
    /// <value>存在時為 <see langword="true"/>。</value>
    public bool IsFFmpegPresent { get; }

    /// <summary>
    /// 取得 ffprobe.exe 是否存在。
    /// </summary>
    /// <value>存在時為 <see langword="true"/>。</value>
    public bool IsFFprobePresent { get; }

    /// <summary>
    /// 取得分析過程蒐集到的錯誤訊息。
    /// </summary>
    /// <value>錯誤訊息集合；通過時為空集合。</value>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// 取得整體執行階段是否處於可用狀態（核心 libmpv 可用）。
    /// </summary>
    /// <value>libmpv 存在且未發現錯誤時為 <see langword="true"/>。</value>
    /// <remarks>
    /// 「Healthy」表示「能播媒體」的最小條件；要判斷「完整 runtime 已就緒」
    /// （含後處理工具）請改用 <see cref="IsComplete"/> 或 <see cref="IsHealthyFor"/>。
    /// </remarks>
    public bool IsHealthy
    {
        get { return IsLibMpvPresent && Errors.Count == 0; }
    }

    /// <summary>
    /// 取得整體執行階段是否為「完整 runtime」，亦即除核心 libmpv 外，
    /// yt-dlp / deno / ffmpeg / ffprobe 等附帶工具也都齊備。
    /// </summary>
    /// <value>核心 libmpv 與全部附帶工具皆就緒時為 <see langword="true"/>。</value>
    /// <remarks>
    /// 適合用來判斷「能下載 URL + 後處理」的場景。若應用程式僅需播放本機檔，
    /// 用 <see cref="IsHealthy"/> 即可；若需自訂必備工具子集，請用 <see cref="IsHealthyFor"/>。
    /// </remarks>
    public bool IsComplete
    {
        get
        {
            return IsHealthy
                && IsYtdlpPresent
                && IsDenoPresent
                && IsFFmpegPresent
                && IsFFprobePresent;
        }
    }

    /// <summary>
    /// 依使用者指定的「必備工具集合」評估執行階段是否符合健康條件。
    /// 任何指定工具缺少即回傳 <see langword="false"/>；核心 libmpv 永遠必備，
    /// 無需在 <paramref name="requiredTools"/> 中重複列出。
    /// </summary>
    /// <param name="requiredTools">必備附帶工具集合。</param>
    /// <returns>所有指定工具皆存在且核心 libmpv 健康時為 <see langword="true"/>。</returns>
    public bool IsHealthyFor(MpvRuntimeTools requiredTools)
    {
        if (!IsHealthy)
        {
            return false;
        }

        if ((requiredTools & MpvRuntimeTools.YtDlp) != 0 && !IsYtdlpPresent)
        {
            return false;
        }

        if ((requiredTools & MpvRuntimeTools.Deno) != 0 && !IsDenoPresent)
        {
            return false;
        }

        if ((requiredTools & MpvRuntimeTools.FFmpeg) != 0 && !IsFFmpegPresent)
        {
            return false;
        }

        if ((requiredTools & MpvRuntimeTools.FFprobe) != 0 && !IsFFprobePresent)
        {
            return false;
        }

        return true;
    }
}
