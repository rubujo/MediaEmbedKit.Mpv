using System.Collections.Generic;

namespace MediaEmbedKit.Mpv
{
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
        /// <value>包含 yt-dlp 或 Deno 等外部工具的資料夾。</value>
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
    }
}
