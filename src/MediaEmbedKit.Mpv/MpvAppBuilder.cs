using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供以 fluent 風格組合 <see cref="MpvPlayer"/> 初始化流程的 builder。
/// 範例：<c>await new MpvAppBuilder().UseWindowsRuntimeAutoInstall().UseYtdlp(...).BuildAsync()</c>。
/// </summary>
public sealed class MpvAppBuilder
{
    /// <summary>
    /// 待 build 時套用到 <see cref="MpvPlayerOptions"/> 的設定動作清單。
    /// </summary>
    private readonly System.Collections.Generic.List<Action<MpvPlayerOptions>> _optionConfigurators;
    /// <summary>
    /// 待 build 時要先執行的 runtime 安裝或準備動作。
    /// </summary>
    private Func<CancellationToken, Task<string?>>? _runtimePreparer;
    /// <summary>
    /// 表示是否要在 runtime 準備完成後把資料夾設定到 <see cref="MpvPlayerOptions"/>。
    /// </summary>
    private bool _applyRuntimeDirectoryToOptions;
    /// <summary>
    /// runtime 準備完成後是否要載入該資料夾的 mpv 設定。
    /// </summary>
    private bool _loadRuntimeConfiguration;

    /// <summary>
    /// 初始化 <see cref="MpvAppBuilder"/> 類別的新執行個體。
    /// </summary>
    public MpvAppBuilder()
    {
        _optionConfigurators = new System.Collections.Generic.List<Action<MpvPlayerOptions>>();
    }

    /// <summary>
    /// 指定要使用的 libmpv 原生程式庫路徑。
    /// </summary>
    /// <param name="libraryPath">
    /// libmpv 檔案路徑或包含 libmpv 的資料夾。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder UseLibrary(string libraryPath)
    {
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            throw new ArgumentException("libmpv 路徑不可為空白。", nameof(libraryPath));
        }

        _optionConfigurators.Add(options => options.UseMpvLibraryPath(libraryPath));
        return this;
    }

    /// <summary>
    /// 將指定 runtime 資料夾直接套用到 <see cref="MpvPlayerOptions"/>，不執行任何安裝動作。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 已備妥 libmpv 與外部工具的執行階段資料夾。
    /// </param>
    /// <param name="loadRuntimeConfiguration">
    /// 是否同時把該資料夾設定為 mpv 設定資料夾。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder UseRuntime(string runtimeDirectory, bool loadRuntimeConfiguration = false)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        _runtimePreparer = _ => Task.FromResult<string?>(runtimeDirectory);
        _applyRuntimeDirectoryToOptions = true;
        _loadRuntimeConfiguration = loadRuntimeConfiguration;
        return this;
    }

    /// <summary>
    /// 內部用於由 <c>MediaEmbedKit.Mpv.Runtime</c> 套件的擴充方法注入 runtime 準備邏輯，
    /// 不揭露 <c>MpvRuntimeInstallOptions</c> 等 .Runtime 型別到核心套件。
    /// </summary>
    /// <param name="preparer">
    /// 執行 runtime 準備動作的委派，返回 runtime 路徑或 <c>null</c>。
    /// </param>
    /// <param name="applyRuntimeDirectoryToOptions">
    /// 完成後是否要把資料夾套用到 player 選項。
    /// </param>
    /// <param name="loadRuntimeConfiguration">
    /// 是否要載入該資料夾的 mpv 設定。
    /// </param>
    internal void SetRuntimePreparer(
        Func<CancellationToken, Task<string?>> preparer,
        bool applyRuntimeDirectoryToOptions,
        bool loadRuntimeConfiguration)
    {
        _runtimePreparer = preparer ?? throw new ArgumentNullException(nameof(preparer));
        _applyRuntimeDirectoryToOptions = applyRuntimeDirectoryToOptions;
        _loadRuntimeConfiguration = loadRuntimeConfiguration;
    }

    /// <summary>
    /// 設定 yt-dlp 格式預設值。
    /// </summary>
    /// <param name="preset">
    /// 要套用的 yt-dlp 格式預設值。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder UseYtdlp(MpvYtdlpFormatPreset preset)
    {
        _optionConfigurators.Add(options => options.UseYtdlpFormat(preset));
        return this;
    }

    /// <summary>
    /// 設定 yt-dlp 格式 selector。
    /// </summary>
    /// <param name="selector">
    /// 要套用的 yt-dlp 格式 selector 字串。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder UseYtdlpFormat(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            throw new ArgumentException("yt-dlp selector 不可為空白。", nameof(selector));
        }

        _optionConfigurators.Add(options => options.UseYtdlpFormat(selector));
        return this;
    }

    /// <summary>
    /// 設定 yt-dlp 最高解析度。
    /// </summary>
    /// <param name="maximumHeight">
    /// 最高高度像素。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder UseYtdlpMaximumHeight(int maximumHeight)
    {
        _optionConfigurators.Add(options => options.UseYtdlpMaximumHeight(maximumHeight));
        return this;
    }

    /// <summary>
    /// 設定 mpv 硬體解碼模式。
    /// </summary>
    /// <param name="mode">
    /// mpv <c>hwdec</c> 值；預設為 <c>auto-safe</c>。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder UseHardwareDecoding(string mode = "auto-safe")
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            throw new ArgumentException("hwdec 模式不可為空白。", nameof(mode));
        }

        _optionConfigurators.Add(options => options.WithInitialOption("hwdec", mode));
        return this;
    }

    /// <summary>
    /// 設定 libmpv 記錄訊息要轉送的 <see cref="ILoggerFactory"/>。
    /// </summary>
    /// <param name="loggerFactory">
    /// 要使用的 <see cref="ILoggerFactory"/>。
    /// </param>
    /// <param name="logLevel">
    /// 要請求 libmpv 的最低記錄等級；預設保留呼叫前的設定。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder UseLogger(ILoggerFactory loggerFactory, string? logLevel = null)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _optionConfigurators.Add(options =>
        {
            options.LoggerFactory = loggerFactory;
            if (!string.IsNullOrWhiteSpace(logLevel))
            {
                options.LogLevel = logLevel!;
            }
        });
        return this;
    }


    /// <summary>
    /// 將 mpv encoding mode 選項套用到 builder 產生的 <see cref="MpvPlayerOptions"/>。
    /// 注意：encoding mode 會接管視訊與音訊輸出；通常與 <see cref="UseHardwareDecoding"/> 共用時需評估解碼路徑是否影響輸出品質。
    /// </summary>
    /// <param name="encodingOptions">
    /// 已配置好的 encoding mode 選項。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="encodingOptions"/> 為 <see langword="null"/> 時擲出。
    /// </exception>
    public MpvAppBuilder UseEncodingTo(MpvEncodingOptions encodingOptions)
    {
        if (encodingOptions == null)
        {
            throw new ArgumentNullException(nameof(encodingOptions));
        }

        _optionConfigurators.Add(options => encodingOptions.ApplyTo(options));
        return this;
    }

    /// <summary>
    /// 取得一個進一步調整 <see cref="MpvPlayerOptions"/> 的入口，用於 builder 未直接支援的選項。
    /// </summary>
    /// <param name="configure">
    /// 要執行的設定動作。
    /// </param>
    /// <returns>
    /// 目前 builder。
    /// </returns>
    public MpvAppBuilder ConfigureOptions(Action<MpvPlayerOptions> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        _optionConfigurators.Add(configure);
        return this;
    }

    /// <summary>
    /// 依目前 builder 設定建立並初始化 <see cref="MpvPlayer"/>。
    /// </summary>
    /// <param name="cancellationToken">
    /// 取消建置的 token。
    /// </param>
    /// <returns>
    /// 已 <see cref="MpvPlayer.Initialize"/> 完成的播放器。
    /// </returns>
    public async Task<MpvPlayer> BuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MpvPlayerOptions options;
        string? runtimeDirectory = null;
        if (_runtimePreparer != null)
        {
            runtimeDirectory = await _runtimePreparer(cancellationToken).ConfigureAwait(false);
            if (_applyRuntimeDirectoryToOptions && !string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                options = new MpvPlayerOptions
                {
                    MpvLibraryPath = Path.Combine(runtimeDirectory!, "libmpv-2.dll"),
                    ToolDirectory = runtimeDirectory,
                    YtdlpPath = Path.Combine(runtimeDirectory!, "yt-dlp.exe") + ";yt-dlp;youtube-dl",
                    EnableYtdlp = true
                };
                if (_loadRuntimeConfiguration)
                {
                    options.ConfigDirectory = runtimeDirectory;
                    options.LoadUserConfig = true;
                }
            }
            else
            {
                options = new MpvPlayerOptions();
            }
        }
        else
        {
            options = new MpvPlayerOptions();
        }

        foreach (Action<MpvPlayerOptions> configurator in _optionConfigurators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            configurator(options);
        }

        MpvPlayer player = new MpvPlayer(options);
        try
        {
            await player.InitializeAsync(cancellationToken).ConfigureAwait(false);
            return player;
        }
        catch
        {
            player.Dispose();
            throw;
        }
    }
}
