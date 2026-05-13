using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供建立 <see cref="MpvPlayer"/> 執行個體時套用的初始選項。
/// </summary>
public sealed class MpvPlayerOptions
{
    /// <summary>
    /// 初始化 <see cref="MpvPlayerOptions"/> 類別的新執行個體。
    /// </summary>
    public MpvPlayerOptions()
    {
        ConfigFiles = new List<string>();
        InitialOptions = new Dictionary<string, string>();
        ScriptFiles = new List<string>();
        YtdlpPath = "yt-dlp;youtube-dl";
        EnableDefaultInputBindings = true;
        EnableKeyboardInput = true;
        EnableOsc = true;
        EnableYtdlp = true;
        YtdlpFormatPreset = MpvYtdlpFormatPreset.Default;
        LogLevel = "warn";
    }

    /// <summary>
    /// 取得或設定要載入的 libmpv 原生程式庫路徑。
    /// </summary>
    /// <value>libmpv 檔案路徑或包含 libmpv 的資料夾；未指定時會使用預設搜尋邏輯。</value>
    public string? MpvLibraryPath { get; set; }

    /// <summary>
    /// 取得建立播放器時要傳給 libmpv 的初始選項集合。
    /// </summary>
    /// <value>以 libmpv 選項名稱為索引鍵的初始選項集合。</value>
    public IDictionary<string, string> InitialOptions { get; private set; }

    /// <summary>
    /// 指定要使用的 libmpv 原生程式庫路徑。
    /// </summary>
    /// <param name="mpvLibraryPath">libmpv 檔案路徑或包含 libmpv 的資料夾。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions UseMpvLibraryPath(string mpvLibraryPath)
    {
        if (string.IsNullOrWhiteSpace(mpvLibraryPath))
        {
            throw new ArgumentException("libmpv 路徑不可為空白。", nameof(mpvLibraryPath));
        }

        MpvLibraryPath = mpvLibraryPath;
        return this;
    }

    /// <summary>
    /// 指定外部工具所在的資料夾。
    /// </summary>
    /// <param name="toolDirectory">包含 yt-dlp、Deno、FFmpeg 或 FFprobe 等工具的資料夾。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions UseToolDirectory(string toolDirectory)
    {
        if (string.IsNullOrWhiteSpace(toolDirectory))
        {
            throw new ArgumentException("外部工具資料夾不可為空白。", nameof(toolDirectory));
        }

        ToolDirectory = toolDirectory;
        return this;
    }

    /// <summary>
    /// 指定 mpv 使用者設定資料夾並啟用設定載入。
    /// </summary>
    /// <param name="configDirectory">mpv 設定資料夾路徑。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions UseRuntimeConfiguration(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            throw new ArgumentException("mpv 設定資料夾不可為空白。", nameof(configDirectory));
        }

        ConfigDirectory = configDirectory;
        LoadUserConfig = true;
        return this;
    }

    /// <summary>
    /// 加入初始化時要設定的 mpv 選項。
    /// </summary>
    /// <param name="name">mpv 選項名稱。</param>
    /// <param name="value">mpv 選項值。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions WithInitialOption(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("mpv 選項名稱不可為空白。", nameof(name));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        InitialOptions[name] = value;
        return this;
    }

    /// <summary>
    /// 加入初始化前要明確載入的 mpv 設定檔。
    /// </summary>
    /// <param name="configFile">mpv 設定檔路徑。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions AddConfigFile(string configFile)
    {
        if (string.IsNullOrWhiteSpace(configFile))
        {
            throw new ArgumentException("mpv 設定檔路徑不可為空白。", nameof(configFile));
        }

        ConfigFiles.Add(configFile);
        return this;
    }

    /// <summary>
    /// 加入初始化前要明確載入的 mpv 腳本檔。
    /// </summary>
    /// <param name="scriptFile">Lua 或 JavaScript 腳本檔案路徑。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions AddScriptFile(string scriptFile)
    {
        if (string.IsNullOrWhiteSpace(scriptFile))
        {
            throw new ArgumentException("mpv 腳本檔案路徑不可為空白。", nameof(scriptFile));
        }

        ScriptFiles.Add(scriptFile);
        return this;
    }

    /// <summary>
    /// 使用常用預設值指定 yt-dlp 格式選擇。
    /// </summary>
    /// <param name="preset">要套用的 yt-dlp 格式選擇預設值。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions UseYtdlpFormat(MpvYtdlpFormatPreset preset)
    {
        MpvYtdlpFormatSelector.FromPreset(preset);
        YtdlpFormatPreset = preset;
        YtdlpFormat = null;
        return this;
    }

    /// <summary>
    /// 使用自訂 selector 指定 yt-dlp 格式選擇。
    /// </summary>
    /// <param name="formatSelector">要傳給 mpv <c>ytdl-format</c> 選項的 selector 字串。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions UseYtdlpFormat(string formatSelector)
    {
        if (formatSelector == null)
        {
            throw new ArgumentNullException(nameof(formatSelector));
        }

        YtdlpFormatPreset = MpvYtdlpFormatPreset.Default;
        YtdlpFormat = string.IsNullOrWhiteSpace(formatSelector) ? null : formatSelector;
        return this;
    }

    /// <summary>
    /// 以最高視訊高度建立 yt-dlp 格式選擇。
    /// </summary>
    /// <param name="maximumHeight">允許的最大視訊高度。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions UseYtdlpMaximumHeight(int maximumHeight)
    {
        YtdlpFormatPreset = MpvYtdlpFormatPreset.Default;
        YtdlpFormat = MpvYtdlpFormatSelector.MaxHeight(maximumHeight);
        return this;
    }

    /// <summary>
    /// 將 mpv encoding mode 選項加入初始選項集合並傳回目前選項。
    /// </summary>
    /// <param name="encodingOptions">要套用的 encoding mode 選項。</param>
    /// <returns>目前的播放器選項。</returns>
    public MpvPlayerOptions UseEncoding(MpvEncodingOptions encodingOptions)
    {
        ConfigureEncoding(encodingOptions);
        return this;
    }

    /// <summary>
    /// 將 mpv encoding mode 選項加入初始選項集合。
    /// </summary>
    /// <param name="encodingOptions">要套用的 encoding mode 選項。</param>
    public void ConfigureEncoding(MpvEncodingOptions encodingOptions)
    {
        if (encodingOptions == null)
        {
            throw new ArgumentNullException(nameof(encodingOptions));
        }

        encodingOptions.ApplyTo(this);
    }

    /// <summary>
    /// 取得或設定是否啟用 libmpv 預設輸入繫結。
    /// </summary>
    /// <value>啟用預設輸入繫結時為 <see langword="true"/>。</value>
    public bool EnableDefaultInputBindings { get; set; }

    /// <summary>
    /// 取得或設定是否允許 libmpv 接收鍵盤輸入。
    /// </summary>
    /// <value>啟用鍵盤輸入時為 <see langword="true"/>。</value>
    public bool EnableKeyboardInput { get; set; }

    /// <summary>
    /// 取得或設定是否啟用 libmpv 螢幕控制項。
    /// </summary>
    /// <value>啟用螢幕控制項時為 <see langword="true"/>。</value>
    public bool EnableOsc { get; set; }

    /// <summary>
    /// 取得或設定是否啟用 mpv 的 yt-dlp 整合。
    /// </summary>
    /// <value>啟用 yt-dlp 支援時為 <see langword="true"/>。</value>
    public bool EnableYtdlp { get; set; }

    /// <summary>
    /// 取得或設定 mpv ytdl_hook 用來尋找 yt-dlp 的路徑清單。
    /// </summary>
    /// <value>以分號分隔的 yt-dlp 或 youtube-dl 可執行檔路徑清單。</value>
    public string YtdlpPath { get; set; }

    /// <summary>
    /// 取得或設定 yt-dlp 格式選擇預設值。
    /// </summary>
    /// <value>常用的 yt-dlp 格式選擇預設值。</value>
    public MpvYtdlpFormatPreset YtdlpFormatPreset { get; set; }

    /// <summary>
    /// 取得或設定自訂 yt-dlp 格式選擇字串。
    /// </summary>
    /// <value>要傳給 mpv <c>ytdl-format</c> 選項的自訂 selector；未指定時使用 <see cref="YtdlpFormatPreset"/>。</value>
    public string? YtdlpFormat { get; set; }

    /// <summary>
    /// 取得或設定 mpv 使用者設定資料夾。
    /// </summary>
    /// <value>mpv 設定資料夾路徑；未指定時使用 mpv 預設值。</value>
    public string? ConfigDirectory { get; set; }

    /// <summary>
    /// 取得初始化前要明確載入的 mpv 設定檔清單。
    /// </summary>
    /// <value>要傳給 libmpv 載入的設定檔路徑集合。</value>
    public IList<string> ConfigFiles { get; private set; }

    /// <summary>
    /// 取得或設定要載入的 mpv 輸入設定檔。
    /// </summary>
    /// <value>input.conf 檔案路徑；未指定時使用 mpv 設定資料夾中的預設位置。</value>
    public string? InputConfigFile { get; set; }

    /// <summary>
    /// 取得或設定是否載入 mpv 腳本。
    /// </summary>
    /// <value>載入腳本時為 <see langword="true"/>；未指定時使用 mpv 預設值。</value>
    public bool? LoadScripts { get; set; }

    /// <summary>
    /// 取得初始化前要明確載入的 Lua 或 JavaScript 腳本檔案清單。
    /// </summary>
    /// <value>要傳給 mpv <c>scripts</c> 選項的腳本檔案路徑集合。</value>
    public IList<string> ScriptFiles { get; private set; }

    /// <summary>
    /// 取得或設定外部工具所在的工作資料夾。
    /// </summary>
    /// <value>包含 yt-dlp、Deno、FFmpeg 或 FFprobe 等外部工具的資料夾。</value>
    public string? ToolDirectory { get; set; }

    /// <summary>
    /// 取得或設定是否載入 mpv 使用者設定。
    /// </summary>
    /// <value>載入使用者設定時為 <see langword="true"/>。</value>
    public bool LoadUserConfig { get; set; }

    /// <summary>
    /// 取得或設定要向 libmpv 訂閱的最低記錄等級。
    /// </summary>
    /// <value>mpv 記錄等級文字，例如 <c>warn</c> 或 <c>info</c>。</value>
    public string LogLevel { get; set; }

    /// <summary>
    /// 取得或設定要把 libmpv 記錄訊息轉送過去的 <see cref="ILoggerFactory"/>。
    /// </summary>
    /// <value>要使用的 <see cref="ILoggerFactory"/>；未設定時不啟用 ILogger 整合。</value>
    /// <remarks>
    /// 若同時設定 <see cref="LogLevel"/> 與此屬性，<see cref="MpvPlayer"/> 會在初始化後
    /// 訂閱 <c>LogMessageReceived</c> 並以對應的 <see cref="LogLevel"/> 轉送到
    /// <see cref="ILogger"/>。
    /// </remarks>
    public ILoggerFactory? LoggerFactory { get; set; }
}
