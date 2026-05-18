using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供依目前平台選擇執行階段安裝流程的 helper。
/// </summary>
public static class MpvRuntimeInstaller
{
    /// <summary>
    /// 安裝或更新目前平台的執行階段資料夾。
    /// </summary>
    /// <param name="runtimeDirectory">要建立或更新的執行階段資料夾。</param>
    /// <param name="options">平台感知執行階段安裝選項；未指定時使用預設選項。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示平台感知執行階段安裝結果的工作。</returns>
    public static async Task<MpvRuntimeInstallResult> InstallOrUpdateAsync(
        string runtimeDirectory,
        MpvRuntimeInstallOptions? options = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        options = options ?? new MpvRuntimeInstallOptions();
        MpvNativeRuntimePlatform platform = options.Platform ?? MpvNativeRuntimeCatalog.CurrentPlatform();

        if (platform == MpvNativeRuntimePlatform.Windows)
        {
            MpvWindowsRuntimeDownloadResult windows = await MpvWindowsRuntimeInstaller.InstallOrUpdateAsync(
                runtimeDirectory,
                options.Windows,
                cancellationToken).ConfigureAwait(false);

            return new MpvRuntimeInstallResult(
                platform,
                MpvNativeRuntimeSupportStatus.Supported,
                runtimeDirectory,
                "Windows 執行階段已安裝或更新。",
                windows,
                MpvNativeRuntimeCatalog.GetSources(platform));
        }

        return new MpvRuntimeInstallResult(
            platform,
            MpvNativeRuntimeCatalog.GetProjectSupportStatus(platform),
            runtimeDirectory,
            "自動執行階段安裝目前僅實作 Windows x64 / ARM64。",
            null,
            MpvNativeRuntimeCatalog.GetSources(platform));
    }

    /// <summary>
    /// 建立指向指定執行階段資料夾的播放器選項。
    /// </summary>
    /// <param name="runtimeDirectory">包含原生 libmpv 與外部工具的執行階段資料夾。</param>
    /// <returns>可用於 <see cref="MpvPlayer"/> 的播放器選項。</returns>
    public static MpvPlayerOptions CreatePlayerOptions(string runtimeDirectory)
    {
        return MpvWindowsRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
    }

    /// <summary>
    /// 建立指向指定執行階段資料夾的播放器選項，並可選擇載入同一資料夾中的 mpv 設定。
    /// </summary>
    /// <param name="runtimeDirectory">包含原生 libmpv、外部工具與 mpv 設定檔的執行階段資料夾。</param>
    /// <param name="loadRuntimeConfiguration">是否將執行階段資料夾設為 mpv 設定資料夾。</param>
    /// <returns>可用於 <see cref="MpvPlayer"/> 的播放器選項。</returns>
    public static MpvPlayerOptions CreatePlayerOptions(string runtimeDirectory, bool loadRuntimeConfiguration)
    {
        return MpvWindowsRuntimeInstaller.CreatePlayerOptions(runtimeDirectory, loadRuntimeConfiguration);
    }
}
