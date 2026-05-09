using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MediaEmbedKit.Mpv.WinForms
{
    /// <summary>
    /// 提供 WinForms 用的 libmpv 播放控制項。
    /// </summary>
    [ToolboxItem(true)]
    [DefaultProperty(nameof(BackColor))]
    [DefaultEvent(nameof(PlayerCreated))]
    [DesignerCategory("Code")]
    public class MpvPlayerControl : Control
    {
        /// <summary>
        /// 目前控制項持有的 mpv 播放器執行個體。
        /// </summary>
        private MpvPlayer? _player;

        /// <summary>
        /// 初始化 <see cref="MpvPlayerControl"/> 類別的新執行個體。
        /// </summary>
        public MpvPlayerControl()
        {
            BackColor = Color.Black;
            PlayerOptions = new MpvPlayerOptions();
            SetStyle(ControlStyles.Opaque, true);
        }

        /// <summary>
        /// 在控制項建立 mpv 播放器後發生。
        /// </summary>
        [Category("MediaEmbedKit.Mpv")]
        public event EventHandler? PlayerCreated;

        /// <summary>
        /// 取得控制項建立播放器時使用的選項。
        /// </summary>
        /// <value>播放器建立選項。</value>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MpvPlayerOptions PlayerOptions { get; private set; }

        /// <summary>
        /// 取得控制項目前建立的播放器。
        /// </summary>
        /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MpvPlayer? Player
        {
            get { return _player; }
        }

        /// <summary>
        /// 載入檔案或網址作為播放項目。
        /// </summary>
        /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
        /// <param name="mode">播放項目加入播放清單的方式。</param>
        public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
        {
            if (IsInDesignMode())
            {
                return;
            }

            EnsurePlayer();
            _player!.LoadFile(pathOrUrl, mode);
        }

        /// <summary>
        /// 在 WinForms 控制項 Handle 建立後初始化播放器。
        /// </summary>
        /// <param name="e">事件資料。</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!IsInDesignMode())
            {
                EnsurePlayer();
            }
        }

        /// <summary>
        /// 在 WinForms 控制項 Handle 銷毀時釋放播放器。
        /// </summary>
        /// <param name="e">事件資料。</param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (!RecreatingHandle)
            {
                DisposePlayer();
            }

            base.OnHandleDestroyed(e);
        }

        /// <summary>
        /// 在 WinForms 設計工具中繪製替代預覽內容。
        /// </summary>
        /// <param name="e">繪製事件資料。</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!IsInDesignMode())
            {
                return;
            }

            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            TextRenderer.DrawText(
                e.Graphics,
                "MediaEmbedKit.Mpv",
                Font,
                ClientRectangle,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        /// <summary>
        /// 釋放控制項使用的受控資源。
        /// </summary>
        /// <param name="disposing">由受控程式碼釋放時為 <see langword="true"/>。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposePlayer();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 確保控制項已建立並初始化 mpv 播放器。
        /// </summary>
        private void EnsurePlayer()
        {
            if (_player != null)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                CreateControl();
            }

            _player = new MpvPlayer(PlayerOptions);
            _player.SetVideoWindow(Handle);
            _player.Initialize();
            PlayerCreated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 釋放目前控制項持有的 mpv 播放器。
        /// </summary>
        private void DisposePlayer()
        {
            if (_player == null)
            {
                return;
            }

            _player.Dispose();
            _player = null;
        }

        /// <summary>
        /// 判斷控制項目前是否在設計工具中執行。
        /// </summary>
        /// <returns>控制項位於設計階段時為 <see langword="true"/>。</returns>
        private bool IsInDesignMode()
        {
            return DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
        }
    }
}
