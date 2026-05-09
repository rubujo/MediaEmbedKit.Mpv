using Microsoft.Maui.Handlers;
#if WINDOWS
using MediaEmbedKit.Mpv.WinUI;
#endif

namespace MediaEmbedKit.Mpv.Maui
{
    /// <summary>
    /// 將 <see cref="MpvView"/> 對應到 Windows 平台的 WinUI AirSpace 安全 mpv 控制項。
    /// </summary>
#if WINDOWS
    public class MpvViewHandler : ViewHandler<MpvView, MpvWinUiPlayer>
#else
    public class MpvViewHandler : ViewHandler<MpvView, object>
#endif
    {
        /// <summary>
        /// 取得 MAUI 屬性對應表。
        /// </summary>
        /// <value>將 <see cref="MpvView"/> 屬性同步到平台控制項的對應表。</value>
        public static readonly IPropertyMapper<MpvView, MpvViewHandler> Mapper =
            new PropertyMapper<MpvView, MpvViewHandler>(ViewMapper)
            {
                [nameof(MpvView.Source)] = MapSource
            };

        /// <summary>
        /// 初始化 <see cref="MpvViewHandler"/> 類別的新執行個體。
        /// </summary>
        public MpvViewHandler()
            : base(Mapper)
        {
        }

        /// <summary>
        /// 透過平台控制項載入檔案或網址。
        /// </summary>
        /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
        /// <param name="mode">播放項目加入播放清單的方式。</param>
        public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
        {
#if WINDOWS
            if (Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled)
            {
                return;
            }

            PlatformView.LoadFile(pathOrUrl, mode);
            VirtualView.SetPlayer(PlatformView.Player);
#else
            throw new PlatformNotSupportedException("此平台尚未提供 MAUI libmpv handler。");
#endif
        }

        /// <summary>
        /// 建立 Windows 平台使用的 WinUI AirSpace 安全 mpv 控制項。
        /// </summary>
        /// <returns>新建立的平台 mpv 控制項。</returns>
#if WINDOWS
        protected override MpvWinUiPlayer CreatePlatformView()
        {
            return new MpvWinUiPlayer();
        }
#else
        protected override object CreatePlatformView()
        {
            throw new PlatformNotSupportedException("此平台尚未提供 MAUI libmpv handler。");
        }
#endif

        /// <summary>
        /// 連接 MAUI 虛擬檢視與平台控制項。
        /// </summary>
        /// <param name="platformView">要連接的平台 mpv 控制項。</param>
#if WINDOWS
        protected override void ConnectHandler(MpvWinUiPlayer platformView)
#else
        protected override void ConnectHandler(object platformView)
#endif
        {
            base.ConnectHandler(platformView);
#if WINDOWS
            if (Microsoft.Maui.Controls.DesignMode.IsDesignModeEnabled)
            {
                return;
            }

            CopyPlayerOptions(platformView);
            AttachPlatformWindow(platformView);
            platformView.PlayerCreated += OnPlayerCreated;
            VirtualView.SetPlayer(platformView.Player);

            if (!string.IsNullOrWhiteSpace(VirtualView.PendingSource))
            {
                platformView.LoadFile(VirtualView.PendingSource!, VirtualView.PendingMode);
            }
#endif
        }

        /// <summary>
        /// 中斷 MAUI 虛擬檢視與平台控制項的連接。
        /// </summary>
        /// <param name="platformView">要中斷連接的平台 mpv 控制項。</param>
#if WINDOWS
        protected override void DisconnectHandler(MpvWinUiPlayer platformView)
#else
        protected override void DisconnectHandler(object platformView)
#endif
        {
#if WINDOWS
            platformView.PlayerCreated -= OnPlayerCreated;
            VirtualView.SetPlayer(null);
            platformView.Dispose();
#endif
            base.DisconnectHandler(platformView);
        }

        /// <summary>
        /// 在平台控制項建立播放器後同步虛擬檢視的播放器參考。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnPlayerCreated(object? sender, System.EventArgs e)
        {
#if WINDOWS
            VirtualView.SetPlayer(PlatformView.Player);
#endif
        }

        /// <summary>
        /// 將 <see cref="MpvView.Source"/> 屬性變更套用到平台控制項。
        /// </summary>
        /// <param name="handler">正在處理屬性對應的 MAUI handler。</param>
        /// <param name="view">來源屬性變更的 MAUI mpv 檢視。</param>
        private static void MapSource(MpvViewHandler handler, MpvView view)
        {
#if WINDOWS
            if (!string.IsNullOrWhiteSpace(view.Source))
            {
                handler.LoadFile(view.Source!);
            }
#endif
        }

#if WINDOWS
        /// <summary>
        /// 將 MAUI 視窗轉接到 WinUI 播放控制項。
        /// </summary>
        /// <param name="platformView">要附加視窗的 Windows 平台控制項。</param>
        private void AttachPlatformWindow(MpvWinUiPlayer platformView)
        {
            Microsoft.Maui.Controls.Window? mauiWindow = VirtualView.Window;
            if (mauiWindow != null && mauiWindow.Handler != null && mauiWindow.Handler.PlatformView is Microsoft.UI.Xaml.Window winUiWindow)
            {
                platformView.Attach(winUiWindow);
            }
        }

        /// <summary>
        /// 將 MAUI 虛擬檢視的播放器選項複製到 Windows 平台控制項。
        /// </summary>
        /// <param name="platformView">要套用設定的 Windows 平台控制項。</param>
        private void CopyPlayerOptions(MpvWinUiPlayer platformView)
        {
            platformView.PlayerOptions.MpvLibraryPath = VirtualView.PlayerOptions.MpvLibraryPath;
            platformView.PlayerOptions.EnableDefaultInputBindings = VirtualView.PlayerOptions.EnableDefaultInputBindings;
            platformView.PlayerOptions.EnableKeyboardInput = VirtualView.PlayerOptions.EnableKeyboardInput;
            platformView.PlayerOptions.EnableOsc = VirtualView.PlayerOptions.EnableOsc;
            platformView.PlayerOptions.EnableYtdlp = VirtualView.PlayerOptions.EnableYtdlp;
            platformView.PlayerOptions.YtdlpPath = VirtualView.PlayerOptions.YtdlpPath;
            platformView.PlayerOptions.YtdlpFormatPreset = VirtualView.PlayerOptions.YtdlpFormatPreset;
            platformView.PlayerOptions.YtdlpFormat = VirtualView.PlayerOptions.YtdlpFormat;
            platformView.PlayerOptions.ConfigDirectory = VirtualView.PlayerOptions.ConfigDirectory;
            platformView.PlayerOptions.InputConfigFile = VirtualView.PlayerOptions.InputConfigFile;
            platformView.PlayerOptions.LoadScripts = VirtualView.PlayerOptions.LoadScripts;
            platformView.PlayerOptions.ToolDirectory = VirtualView.PlayerOptions.ToolDirectory;
            platformView.PlayerOptions.LoadUserConfig = VirtualView.PlayerOptions.LoadUserConfig;
            platformView.PlayerOptions.LogLevel = VirtualView.PlayerOptions.LogLevel;
            platformView.PlayerOptions.ConfigFiles.Clear();
            foreach (string configFile in VirtualView.PlayerOptions.ConfigFiles)
            {
                platformView.PlayerOptions.ConfigFiles.Add(configFile);
            }

            platformView.PlayerOptions.ScriptFiles.Clear();
            foreach (string scriptFile in VirtualView.PlayerOptions.ScriptFiles)
            {
                platformView.PlayerOptions.ScriptFiles.Add(scriptFile);
            }

            platformView.PlayerOptions.InitialOptions.Clear();
            foreach (System.Collections.Generic.KeyValuePair<string, string> option in VirtualView.PlayerOptions.InitialOptions)
            {
                platformView.PlayerOptions.InitialOptions[option.Key] = option.Value;
            }
        }
#endif
    }
}
