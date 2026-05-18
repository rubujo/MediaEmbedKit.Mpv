using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供 <see cref="MpvAppBuilder"/> 的 runtime 安裝整合擴充方法。
/// 拆 package 前此方法位於 <c>MpvAppBuilder</c> 自身，移到 <c>MediaEmbedKit.Mpv.Runtime</c>
/// 套件後核心套件不再依賴 <c>MpvRuntimeInstaller</c> 等 runtime 型別。
/// </summary>
public static class MpvAppBuilderRuntimeExtensions
{
    /// <summary>
    /// 安裝或更新 Windows x64 / ARM64 runtime 後再用其結果建立播放器選項。
    /// </summary>
    /// <param name="builder">要設定的 <see cref="MpvAppBuilder"/>。</param>
    /// <param name="runtimeDirectory">要建立或更新的執行階段資料夾。</param>
    /// <param name="configure">可進一步調整 <see cref="MpvRuntimeInstallOptions"/> 的委派。</param>
    /// <returns>傳入的 builder（fluent 風格）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> 為 <c>null</c>。</exception>
    /// <exception cref="ArgumentException"><paramref name="runtimeDirectory"/> 為 <c>null</c> 或空白。</exception>
    public static MpvAppBuilder UseWindowsRuntimeAutoInstall(
        this MpvAppBuilder builder,
        string runtimeDirectory,
        Action<MpvRuntimeInstallOptions>? configure = null)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        builder.SetRuntimePreparer(
            async cancellationToken =>
            {
                MpvRuntimeInstallOptions installOptions = new MpvRuntimeInstallOptions();
                configure?.Invoke(installOptions);
                MpvRuntimeInstallResult result = await MpvRuntimeInstaller.InstallOrUpdateAsync(
                    runtimeDirectory,
                    installOptions,
                    cancellationToken).ConfigureAwait(false);
                return result.IsSupported ? runtimeDirectory : null;
            },
            applyRuntimeDirectoryToOptions: true,
            loadRuntimeConfiguration: false);
        return builder;
    }
}
