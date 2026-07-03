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
/// 提供執行階段資料夾中 libmpv 與 FFmpeg 授權狀態的稽核工具。
/// </summary>
/// <remarks>
/// 稽核器依 mpv 與 FFmpeg 自報的編譯設定字串分類授權狀態，並回報整體散發風險。
/// 解析失敗時對應欄位會回 <see cref="MpvBuildLicense.Unknown"/>，不擲例外。
/// </remarks>
public static class MpvLicenseAuditor
{
    /// <summary>
    /// libmpv 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string LibMpvFileName = "libmpv-2.dll";
    /// <summary>
    /// FFmpeg 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string FFmpegFileName = "ffmpeg.exe";

    /// <summary>
    /// 分析執行階段資料夾中的 libmpv 與 FFmpeg 授權狀態。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ 副作用警示</strong>：<paramref name="probeLibMpv"/> = <see langword="true"/>
    /// 時本方法會透過 <c>NativeLibrary.Load</c> 載入 <c>libmpv-2.dll</c> 讀取
    /// <c>mpv-configuration</c> 屬性。<strong>libmpv 一旦載入當前處理序就無法 unload</strong>，
    /// 之後若要 update libmpv 必須走 暫存更新 + 處理序重啟流程
    /// （參見 <see cref="MpvLibraryUpdateScheduler"/>）。
    /// </para>
    /// <para>
    /// 對僅需檢查 FFmpeg 授權的情境，請保持 <paramref name="probeLibMpv"/> =
    /// <see langword="false"/>（預設）—— 方法仍會用 <c>ffmpeg -version</c> 子處理序
    /// 取得 FFmpeg 授權字串，不會載入 libmpv。
    /// </para>
    /// <para>
    /// 需要 libmpv 授權檢查但不想污染當前處理序，建議在獨立子處理序中呼叫本方法 +
    /// <paramref name="probeLibMpv"/> = <see langword="true"/>。
    /// </para>
    /// </remarks>
    /// <param name="runtimeDirectory">
    /// 要分析的執行階段資料夾。
    /// </param>
    /// <param name="probeLibMpv">
    /// 是否實際載入 libmpv 並讀取 <c>mpv-configuration</c> 屬性。<strong>啟用即不可逆
    /// 載入 libmpv 至當前處理序</strong>，見上方 remarks。
    /// </param>
    /// <param name="cancellationToken">
    /// 取消分析的 token。
    /// </param>
    /// <returns>
    /// 稽核報告。
    /// </returns>
    public static async Task<MpvLicenseAuditReport> AnalyzeAsync(
        string runtimeDirectory,
        bool probeLibMpv = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        string fullPath = Path.GetFullPath(runtimeDirectory);
        string libMpvPath = Path.Combine(fullPath, LibMpvFileName);
        string ffmpegPath = Path.Combine(fullPath, FFmpegFileName);

        List<string> warnings = new List<string>();

        string mpvConfiguration = string.Empty;
        MpvBuildLicense libMpvLicense = MpvBuildLicense.Unknown;
        if (File.Exists(libMpvPath))
        {
            bool libMpvPathTrusted = true;
            try
            {
                RejectIfReparsePoint(libMpvPath, "MpvLicenseAuditor libmpv path");
            }
            catch (InvalidOperationException ex)
            {
                warnings.Add("libmpv 路徑安全檢查失敗：" + ex.Message);
                libMpvPathTrusted = false;
            }

            if (libMpvPathTrusted && probeLibMpv && !MpvLibraryLoader.IsLoaded)
            {
                try
                {
                    MpvLibraryLoader.Load(libMpvPath);
                    using (MpvPlayer player = new MpvPlayer(new MpvPlayerOptions { MpvLibraryPath = libMpvPath }))
                    {
                        player.Initialize();
                        MpvCapabilities capabilities = player.GetCapabilities();
                        mpvConfiguration = capabilities.MpvConfiguration;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add("無法載入 libmpv 取得 mpv-configuration：" + ex.Message);
                }
            }

            if (libMpvPathTrusted)
            {
                libMpvLicense = ClassifyMpvLicense(mpvConfiguration);
            }
        }
        else
        {
            warnings.Add("找不到 " + LibMpvFileName + "。");
        }

        string ffmpegVersionText = string.Empty;
        MpvBuildLicense ffmpegLicense = MpvBuildLicense.Unknown;
        if (File.Exists(ffmpegPath))
        {
            try
            {
                RejectIfReparsePoint(ffmpegPath, "MpvLicenseAuditor ffmpeg path");
                ExternalToolProcessRunner runner = new ExternalToolProcessRunner(ffmpegPath);
                ExternalToolProcessResult result = await runner.RunAsync(
                    new[] { "-hide_banner", "-version" },
                    TimeSpan.FromSeconds(5),
                    cancellationToken).ConfigureAwait(false);
                ffmpegVersionText = result.StandardOutput;
                ffmpegLicense = ClassifyFFmpegLicense(ffmpegVersionText);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                warnings.Add("無法執行 ffmpeg -version：" + ex.Message);
            }
        }
        else
        {
            warnings.Add("找不到 " + FFmpegFileName + "。");
        }

        MpvBuildLicense overall = CombineLicenses(libMpvLicense, ffmpegLicense);
        return new MpvLicenseAuditReport(
            fullPath,
            libMpvLicense,
            ffmpegLicense,
            overall,
            mpvConfiguration,
            ffmpegVersionText,
            new ReadOnlyCollection<string>(warnings));
    }

    /// <summary>
    /// 依 mpv-configuration 字串分類 libmpv 建置授權狀態。
    /// </summary>
    /// <param name="configuration">
    /// mpv-configuration 屬性內容。
    /// </param>
    /// <returns>
    /// 分類後的授權狀態。
    /// </returns>
    internal static MpvBuildLicense ClassifyMpvLicense(string configuration)
    {
        return ClassifyByConfigurationText(configuration);
    }

    /// <summary>
    /// 依 <c>ffmpeg -version</c> 輸出分類 FFmpeg 建置授權狀態。
    /// </summary>
    /// <param name="versionText">
    /// ffmpeg -version 標準輸出。
    /// </param>
    /// <returns>
    /// 分類後的授權狀態。
    /// </returns>
    internal static MpvBuildLicense ClassifyFFmpegLicense(string versionText)
    {
        return ClassifyByConfigurationText(versionText);
    }

    /// <summary>
    /// 依 mpv / FFmpeg 建置設定文字（同樣的 configure flag substring）分類授權狀態。
    /// libmpv 與 FFmpeg 採用相同的 GNU autoconf 風格 <c>--enable-*</c> 旗標慣例，
    /// 兩者的判定邏輯可共用單一輔助方法。
    /// </summary>
    /// <param name="configurationText">
    /// mpv-configuration 屬性或 <c>ffmpeg -version</c> 輸出。
    /// </param>
    /// <returns>
    /// 分類後的授權狀態。
    /// </returns>
    private static MpvBuildLicense ClassifyByConfigurationText(string configurationText)
    {
        if (string.IsNullOrWhiteSpace(configurationText))
        {
            return MpvBuildLicense.Unknown;
        }

        string lower = configurationText.ToLowerInvariant();
        if (lower.Contains("--enable-nonfree") || lower.Contains("--enable-gpl-and-nonfree"))
        {
            return MpvBuildLicense.NonFree;
        }

        if (lower.Contains("--enable-gpl"))
        {
            return MpvBuildLicense.Gpl;
        }

        if (lower.Contains("--enable-lgpl"))
        {
            return MpvBuildLicense.Lgpl;
        }

        return MpvBuildLicense.Unknown;
    }

    /// <summary>
    /// 拒絕指定路徑為 symlink / NTFS reparse point。
    /// </summary>
    /// <param name="path">
    /// 要檢查的路徑。
    /// </param>
    /// <param name="contextDescription">
    /// 擲例外時包含的脈絡文字。
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// 路徑為 reparse point 或無法安全驗證。
    /// </exception>
    private static void RejectIfReparsePoint(string path, string contextDescription)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                "無法讀取路徑屬性以驗證 reparse point：" + path + "（context: " + contextDescription + "）。", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                "無權限讀取路徑屬性：" + path + "（context: " + contextDescription + "）。", ex);
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "偵測到 symlink / NTFS reparse point，拒絕啟動外部程序：" + path +
                "（context: " + contextDescription + "）。");
        }
    }

    /// <summary>
    /// 合併 libmpv 與 FFmpeg 的授權狀態為整體判定。
    /// </summary>
    /// <param name="libMpv">
    /// libmpv 授權狀態。
    /// </param>
    /// <param name="ffmpeg">
    /// FFmpeg 授權狀態。
    /// </param>
    /// <returns>
    /// 整體授權狀態；以最嚴格者為準。
    /// </returns>
    internal static MpvBuildLicense CombineLicenses(MpvBuildLicense libMpv, MpvBuildLicense ffmpeg)
    {
        if (libMpv == MpvBuildLicense.NonFree || ffmpeg == MpvBuildLicense.NonFree)
        {
            return MpvBuildLicense.NonFree;
        }

        if (libMpv == MpvBuildLicense.Gpl || ffmpeg == MpvBuildLicense.Gpl)
        {
            return MpvBuildLicense.Gpl;
        }

        if (libMpv == MpvBuildLicense.Lgpl && ffmpeg == MpvBuildLicense.Lgpl)
        {
            return MpvBuildLicense.Lgpl;
        }

        if (libMpv == MpvBuildLicense.Unknown && ffmpeg == MpvBuildLicense.Unknown)
        {
            return MpvBuildLicense.Unknown;
        }

        return libMpv == MpvBuildLicense.Unknown ? ffmpeg : libMpv;
    }
}

/// <summary>
/// 表示由 <see cref="MpvLicenseAuditor"/> 分類的建置授權狀態。
/// </summary>
public enum MpvBuildLicense
{
    /// <summary>
    /// 無法判定授權狀態。
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// 建置採用 LGPL 相容設定。
    /// </summary>
    Lgpl = 1,
    /// <summary>
    /// 建置採用 GPL 相容設定。
    /// </summary>
    Gpl = 2,
    /// <summary>
    /// 建置包含 nonfree 元件，散發條件最嚴格。
    /// </summary>
    NonFree = 3
}

/// <summary>
/// 由 <see cref="MpvLicenseAuditor"/> 產生的稽核報告。
/// </summary>
public sealed class MpvLicenseAuditReport
{
    /// <summary>
    /// 初始化 <see cref="MpvLicenseAuditReport"/> 類別的新執行個體。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 被分析的執行階段資料夾。
    /// </param>
    /// <param name="libMpvLicense">
    /// libmpv 建置授權狀態。
    /// </param>
    /// <param name="ffmpegLicense">
    /// FFmpeg 建置授權狀態。
    /// </param>
    /// <param name="overallLicense">
    /// 整體判定授權狀態。
    /// </param>
    /// <param name="mpvConfiguration">
    /// 解析 libmpv 時使用的 mpv-configuration 字串。
    /// </param>
    /// <param name="ffmpegVersionText">
    /// 解析 FFmpeg 時使用的版本輸出。
    /// </param>
    /// <param name="warnings">
    /// 分析過程中蒐集的警告。
    /// </param>
    internal MpvLicenseAuditReport(
        string runtimeDirectory,
        MpvBuildLicense libMpvLicense,
        MpvBuildLicense ffmpegLicense,
        MpvBuildLicense overallLicense,
        string mpvConfiguration,
        string ffmpegVersionText,
        IReadOnlyList<string> warnings)
    {
        RuntimeDirectory = runtimeDirectory;
        LibMpvLicense = libMpvLicense;
        FFmpegLicense = ffmpegLicense;
        OverallLicense = overallLicense;
        MpvConfiguration = mpvConfiguration;
        FFmpegVersionText = ffmpegVersionText;
        Warnings = warnings;
    }

    /// <summary>
    /// 取得被分析的執行階段資料夾。
    /// </summary>
    /// <value>
    ///執行階段資料夾完整路徑。
    /// </value>
    public string RuntimeDirectory { get; }

    /// <summary>
    /// 取得 libmpv 建置授權狀態。
    /// </summary>
    /// <value>
    /// 分類後的 libmpv 授權狀態。
    /// </value>
    public MpvBuildLicense LibMpvLicense { get; }

    /// <summary>
    /// 取得 FFmpeg 建置授權狀態。
    /// </summary>
    /// <value>
    /// 分類後的 FFmpeg 授權狀態。
    /// </value>
    public MpvBuildLicense FFmpegLicense { get; }

    /// <summary>
    /// 取得整體判定授權狀態。
    /// </summary>
    /// <value>
    /// 以兩個來源中較嚴格者為準的整體狀態。
    /// </value>
    public MpvBuildLicense OverallLicense { get; }

    /// <summary>
    /// 取得分析 libmpv 時使用的 mpv-configuration 字串。
    /// </summary>
    /// <value>
    /// mpv-configuration 屬性內容；未取得時為空字串。
    /// </value>
    public string MpvConfiguration { get; }

    /// <summary>
    /// 取得分析 FFmpeg 時使用的版本輸出。
    /// </summary>
    /// <value>
    /// <c>ffmpeg -version</c> 的標準輸出；未取得時為空字串。
    /// </value>
    public string FFmpegVersionText { get; }

    /// <summary>
    /// 取得分析過程中蒐集的警告。
    /// </summary>
    /// <value>
    /// 警告訊息集合；分析完全成功時為空。
    /// </value>
    public IReadOnlyList<string> Warnings { get; }
}
