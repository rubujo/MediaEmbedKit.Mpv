using Microsoft.Maui.Controls;

using MediaEmbedKit.Mpv.WinUI;

namespace MediaEmbedKit.Mpv.Maui
{
    /// <summary>
    /// 提供 .NET MAUI Windows 預覽用的 libmpv 播放檢視。
    /// </summary>
    public class MpvView : View
    {
        /// <summary>
        /// 識別 <see cref="Source"/> 可繫結屬性。
        /// </summary>
        public static readonly BindableProperty SourceProperty = BindableProperty.Create(
            nameof(Source),
            typeof(string),
            typeof(MpvView),
            default(string),
            propertyChanged: OnSourceChanged);

        /// <summary>
        /// 保存尚未交給平台 handler 的載入模式。
        /// </summary>
        private MpvLoadFileMode _pendingMode = MpvLoadFileMode.Replace;

        /// <summary>
        /// 初始化 <see cref="MpvView"/> 類別的新執行個體。
        /// </summary>
        public MpvView()
        {
            PlayerOptions = new MpvPlayerOptions();
        }

        /// <summary>
        /// 取得建立播放器時使用的選項。
        /// </summary>
        /// <value>播放器建立選項。</value>
        public MpvPlayerOptions PlayerOptions { get; private set; }

        /// <summary>
        /// 取得目前平台 handler 建立的播放器。
        /// </summary>
        /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
        public MpvPlayer? Player { get; internal set; }

        /// <summary>
        /// 取得目前是否在 MAUI 設計工具中執行。
        /// </summary>
        /// <value>檢視位於設計階段時為 <see langword="true"/>。</value>
        public bool IsDesignMode
        {
            get { return DesignMode.IsDesignModeEnabled; }
        }

        /// <summary>
        /// 取得或設定要載入的媒體來源。
        /// </summary>
        /// <value>檔案路徑或媒體網址。</value>
        public string? Source
        {
            get { return (string?)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        /// <summary>
        /// 取得等待平台 handler 建立後載入的媒體來源。
        /// </summary>
        /// <value>等待載入的檔案路徑或媒體網址。</value>
        internal string? PendingSource { get; private set; }

        /// <summary>
        /// 取得等待平台 handler 建立後套用的載入模式。
        /// </summary>
        /// <value>等待套用的載入模式。</value>
        internal MpvLoadFileMode PendingMode
        {
            get { return _pendingMode; }
        }

        /// <summary>
        /// 載入檔案或網址作為播放項目。
        /// </summary>
        /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
        /// <param name="mode">播放項目加入播放清單的方式。</param>
        public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
        {
            PendingSource = pathOrUrl;
            _pendingMode = mode;

            if (DesignMode.IsDesignModeEnabled)
            {
                return;
            }

            if (Handler is MpvViewHandler handler)
            {
                handler.LoadFile(pathOrUrl, mode);
            }
        }

        /// <summary>
        /// 在 <see cref="Source"/> 屬性變更時載入新的媒體來源。
        /// </summary>
        /// <param name="bindable">屬性所屬的可繫結物件。</param>
        /// <param name="oldValue">屬性先前的值。</param>
        /// <param name="newValue">屬性新的值。</param>
        private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            MpvView view = (MpvView)bindable;
            string? source = newValue as string;
            if (!string.IsNullOrWhiteSpace(source))
            {
                view.LoadFile(source!);
            }
        }

    }
}
