using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using MediaEmbedKit.Mpv.Render;

namespace MediaEmbedKit.Mpv.Avalonia
{
    /// <summary>
    /// 提供以 Avalonia OpenGL render API 組合的 libmpv 播放控制項。
    /// </summary>
    public sealed class MpvAvaloniaPlayer : OpenGlControlBase, IDisposable
    {
        /// <summary>
        /// 目前控制項持有的 mpv 播放器執行個體。
        /// </summary>
        private MpvPlayer? _player;
        /// <summary>
        /// 目前控制項持有的 libmpv OpenGL render API 內容。
        /// </summary>
        private MpvOpenGlRenderContext? _renderContext;
        /// <summary>
        /// 等待 OpenGL render API 內容建立後載入的媒體來源。
        /// </summary>
        private string? _pendingSource;
        /// <summary>
        /// 等待 OpenGL render API 內容建立後套用的載入模式。
        /// </summary>
        private MpvLoadFileMode _pendingMode = MpvLoadFileMode.Replace;
        /// <summary>
        /// 表示目前是否已將下一次轉譯排入 Avalonia UI 佇列。
        /// </summary>
        private bool _renderQueued;
        /// <summary>
        /// 表示目前控制項是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="MpvAvaloniaPlayer"/> 類別的新執行個體。
        /// </summary>
        public MpvAvaloniaPlayer()
        {
            PlayerOptions = new MpvPlayerOptions();
            if (Design.IsDesignMode)
            {
                Design.SetPreviewWith(this, CreateDesignPreview());
            }
        }

        /// <summary>
        /// 在控制項建立 mpv 播放器後發生。
        /// </summary>
        public event EventHandler? PlayerCreated;

        /// <summary>
        /// 取得控制項建立播放器時使用的選項。
        /// </summary>
        /// <value>播放器建立選項。</value>
        public MpvPlayerOptions PlayerOptions { get; private set; }

        /// <summary>
        /// 取得控制項目前建立的播放器。
        /// </summary>
        /// <value>目前播放器；尚未建立時為 <see langword="null"/>。</value>
        public MpvPlayer? Player
        {
            get { return _player; }
        }

        /// <summary>
        /// 取得目前是否已建立 libmpv OpenGL render API 內容。
        /// </summary>
        /// <value>OpenGL render API 內容已建立時為 <see langword="true"/>。</value>
        public bool IsRenderContextCreated
        {
            get { return _renderContext != null; }
        }

        /// <summary>
        /// 取得等待載入的媒體來源。
        /// </summary>
        /// <value>等待載入的檔案路徑或媒體網址；沒有待載入項目時為 <see langword="null"/>。</value>
        public string? PendingSource
        {
            get { return _pendingSource; }
        }

        /// <summary>
        /// 載入檔案或網址作為播放項目。
        /// </summary>
        /// <param name="pathOrUrl">要載入的檔案路徑或媒體網址。</param>
        /// <param name="mode">播放項目加入播放清單的方式。</param>
        public void LoadFile(string pathOrUrl, MpvLoadFileMode mode = MpvLoadFileMode.Replace)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                throw new ArgumentException("媒體來源不可為空白。", nameof(pathOrUrl));
            }

            _pendingSource = pathOrUrl;
            _pendingMode = mode;

            if (Design.IsDesignMode)
            {
                return;
            }

            if (_player != null && _player.IsInitialized)
            {
                _player.LoadFile(pathOrUrl, mode);
            }
        }

        /// <summary>
        /// 釋放控制項持有的播放器與 OpenGL render API 內容。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeRenderContext();
            DisposePlayer();
        }

        /// <summary>
        /// 在 Avalonia 建立 OpenGL 內容時建立 libmpv render API 內容。
        /// </summary>
        /// <param name="gl">Avalonia 提供的 OpenGL 函式介面。</param>
        protected override void OnOpenGlInit(GlInterface gl)
        {
            if (Design.IsDesignMode)
            {
                return;
            }

            EnsureNotDisposed();
            EnsurePlayerAndRenderContext(gl);
        }

        /// <summary>
        /// 在 Avalonia 銷毀 OpenGL 內容時釋放 libmpv render API 內容。
        /// </summary>
        /// <param name="gl">Avalonia 提供的 OpenGL 函式介面。</param>
        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            DisposeRenderContext();
            DisposePlayer();
        }

        /// <summary>
        /// 在 Avalonia OpenGL 內容遺失時釋放 libmpv render API 內容。
        /// </summary>
        protected override void OnOpenGlLost()
        {
            DisposeRenderContext();
            DisposePlayer();
        }

        /// <summary>
        /// 將 libmpv 目前影格轉譯到 Avalonia 提供的 OpenGL framebuffer。
        /// </summary>
        /// <param name="gl">Avalonia 提供的 OpenGL 函式介面。</param>
        /// <param name="fb">Avalonia 提供的 OpenGL framebuffer 物件識別碼。</param>
        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (Design.IsDesignMode || _renderContext == null || _disposed)
            {
                return;
            }

            _renderQueued = false;
            _renderContext.Update();

            double renderScaling = GetRenderScaling();
            int width = Math.Max(1, (int)Math.Round(Bounds.Width * renderScaling));
            int height = Math.Max(1, (int)Math.Round(Bounds.Height * renderScaling));
            _renderContext.Render(fb, width, height, flipY: true);
            _renderContext.ReportSwap();
        }

        /// <summary>
        /// 取得目前視覺根節點使用的實體像素縮放倍率。
        /// </summary>
        /// <returns>實體像素相對於邏輯像素的縮放倍率。</returns>
        private double GetRenderScaling()
        {
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            return topLevel == null ? 1.0 : topLevel.RenderScaling;
        }

        /// <summary>
        /// 確保播放器與 OpenGL render API 內容已建立。
        /// </summary>
        /// <param name="gl">Avalonia 提供的 OpenGL 函式介面。</param>
        private void EnsurePlayerAndRenderContext(GlInterface gl)
        {
            if (Design.IsDesignMode || _renderContext != null)
            {
                return;
            }

            MpvPlayer? player = null;
            MpvOpenGlRenderContext? renderContext = null;
            try
            {
                player = new MpvPlayer(PlayerOptions);
                player.SetOptionString("vo", "libmpv");
                player.Initialize();

                MpvOpenGlRenderContextOptions options = new MpvOpenGlRenderContextOptions(gl.GetProcAddress);
                renderContext = player.CreateOpenGlRenderContext(options);
                renderContext.UpdateAvailable += RenderContextUpdateAvailable;

                _player = player;
                _renderContext = renderContext;
                player = null;
                renderContext = null;
            }
            catch
            {
                if (renderContext != null)
                {
                    renderContext.Dispose();
                }

                if (player != null)
                {
                    player.Dispose();
                }

                throw;
            }

            PlayerCreated?.Invoke(this, EventArgs.Empty);

            if (_player != null && !string.IsNullOrWhiteSpace(_pendingSource))
            {
                _player.LoadFile(_pendingSource!, _pendingMode);
            }
        }

        /// <summary>
        /// 建立 Avalonia 預覽器使用的替代預覽控制項。
        /// </summary>
        /// <returns>可由 Avalonia 預覽器顯示的替代控制項。</returns>
        private static Control CreateDesignPreview()
        {
            TextBlock textBlock = new TextBlock
            {
                Text = "MediaEmbedKit.Mpv Avalonia",
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            Border border = new Border
            {
                Background = Brushes.Black,
                Child = textBlock
            };

            return border;
        }

        /// <summary>
        /// 處理 libmpv render API 更新通知並要求 Avalonia 排入下一個影格。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void RenderContextUpdateAvailable(object? sender, EventArgs e)
        {
            if (_disposed || _renderQueued)
            {
                return;
            }

            _renderQueued = true;
            Dispatcher.UIThread.Post(RequestRenderIfAlive);
        }

        /// <summary>
        /// 在 UI 執行緒上要求 Avalonia 轉譯下一個 OpenGL 影格。
        /// </summary>
        private void RequestRenderIfAlive()
        {
            if (_disposed || _renderContext == null)
            {
                _renderQueued = false;
                return;
            }

            RequestNextFrameRendering();
        }

        /// <summary>
        /// 釋放 libmpv OpenGL render API 內容。
        /// </summary>
        private void DisposeRenderContext()
        {
            if (_renderContext == null)
            {
                return;
            }

            _renderContext.UpdateAvailable -= RenderContextUpdateAvailable;
            _renderContext.Dispose();
            _renderContext = null;
            _renderQueued = false;
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
        /// 確認目前控制項尚未釋放。
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
