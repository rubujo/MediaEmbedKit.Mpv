using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Samples;
using MediaEmbedKit.Mpv.WinForms;

namespace MediaEmbedKit.Mpv.Samples.WinForms
{
    /// <summary>
    /// 表示 WinForms 範例的主要視窗。
    /// </summary>
    public sealed class MainForm : Form
    {
        /// <summary>
        /// 範例事件輸出的最大保留列數。
        /// </summary>
        private const int EventLogLimit = 60;

        /// <summary>
        /// 顯示 libmpv 視訊內容的 WinForms 控制項。
        /// </summary>
        private readonly MpvPlayerControl _player;
        /// <summary>
        /// 讓使用者輸入檔案路徑或媒體網址的文字方塊。
        /// </summary>
        private readonly TextBox _urlTextBox;
        /// <summary>
        /// 載入目前媒體來源的按鈕。
        /// </summary>
        private readonly Button _loadButton;
        /// <summary>
        /// 切換目前播放器暫停狀態的按鈕。
        /// </summary>
        private readonly Button _pauseButton;
        /// <summary>
        /// 停止目前播放項目的按鈕。
        /// </summary>
        private readonly Button _stopButton;
        /// <summary>
        /// 選擇 yt-dlp 格式預設值的下拉選單。
        /// </summary>
        private readonly ComboBox _formatComboBox;
        /// <summary>
        /// 顯示目前播放狀態的標籤。
        /// </summary>
        private readonly Label _statusLabel;
        /// <summary>
        /// 顯示 libmpv 事件與範例生命週期的清單。
        /// </summary>
        private readonly ListBox _eventListBox;
        /// <summary>
        /// 背景讀取並批次套用狀態列文字的分派器。
        /// </summary>
        private readonly SampleStatusUpdateDispatcher _statusDispatcher;
        /// <summary>
        /// 範例進階功能控制器。
        /// </summary>
        private readonly SampleFeatureController _features;
        /// <summary>
        /// 將播放器事件轉接到範例事件清單。
        /// </summary>
        private SamplePlayerEventBridge? _eventBridge;
        /// <summary>
        /// 目前已建立的播放器。
        /// </summary>
        private MpvPlayer? _currentPlayer;
        /// <summary>
        /// 批次轉送事件文字到 UI 執行緒的分派器。
        /// </summary>
        private readonly SampleEventLogDispatcher _eventLogDispatcher;
        /// <summary>
        /// 需要在 runtime 就緒後才可使用的功能按鈕清單。
        /// </summary>
        private readonly List<Button> _featureButtons = new List<Button>();
        /// <summary>
        /// 紀錄範例 runtime 是否已完成初始化。
        /// </summary>
        private bool _runtimeReady;
        /// <summary>
        /// 控制非同步範例功能不可重入的閘門。
        /// </summary>
        private readonly SampleAsyncFeatureGate _asyncFeatureGate = new SampleAsyncFeatureGate();

        /// <summary>
        /// 初始化 <see cref="MainForm"/> 類別的新執行個體。
        /// </summary>
        public MainForm()
        {
            Text = "MediaEmbedKit.Mpv WinForms Sample";
            ClientSize = new Size(SampleRuntime.SampleWindowWidth, SampleRuntime.SampleWindowHeight);
            StartPosition = FormStartPosition.CenterScreen;

            _urlTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = SampleRuntime.PlaybackUrl,
                AutoSize = false,
                Height = SampleRuntime.SampleButtonHeight,
                Margin = new Padding(0, 2, SampleRuntime.SampleControlSpacing, 0)
            };

            _loadButton = CreateCommandButton("Load");
            _loadButton.Click += LoadButtonClick;
            _pauseButton = CreateCommandButton("Pause");
            _pauseButton.Click += PauseButtonClick;
            _stopButton = CreateCommandButton("Stop");
            _stopButton.Margin = new Padding(0);
            _stopButton.Click += StopButtonClick;

            _player = new MpvPlayerControl
            {
                Dock = DockStyle.Fill,
                AutoInitialize = false
            };
            _player.PlayerCreated += PlayerCreated;

            _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
            _formatComboBox = CreateFormatComboBox();
            _statusLabel = new Label
            {
                AutoSize = false,
                Width = 380,
                Height = SampleRuntime.SampleButtonHeight,
                Text = "播放器尚未初始化",
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, SampleRuntime.SampleControlSpacing, 0)
            };

            _eventListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 8.25f),
                HorizontalScrollbar = true,
                IntegralHeight = false
            };

            _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
            _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);

            Controls.Add(CreateRootLayout());
            SetPlaybackControlsEnabled(false);
            Shown += MainFormShown;
            AppendEventLine(CreateLifecycleLine("FormCreated", "範例視窗已建立，等待 runtime 初始化。"));
        }

        /// <summary>
        /// 釋放範例視窗所使用的受控與非受控資源。
        /// </summary>
        /// <param name="disposing">正在釋放受控資源時為 <see langword="true"/>。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusDispatcher.Dispose();
                _eventBridge?.WriteLifecycle("FormDispose", "視窗正在釋放，準備取消事件訂閱。");
                _eventBridge?.Dispose();
                _eventLogDispatcher.Dispose();
                _player.PlayerCreated -= PlayerCreated;
                _currentPlayer = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 在視窗第一次顯示時載入預設播放範例。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void MainFormShown(object? sender, EventArgs e)
        {
            try
            {
                AppendEventLine(CreateLifecycleLine("Shown", "視窗已顯示，準備初始化範例 runtime。"));
                bool initialized = await InitializeRuntimeAsync().ConfigureAwait(true);
                if (!initialized)
                {
                    return;
                }

                AppendEventLine(CreateLifecycleLine("Shown", "runtime 已完成，準備載入預設媒體來源。"));
                LoadCurrentSource();
                if (SampleRuntime.IsFeatureSmokeTestEnabled)
                {
                    await RunFeatureSmokeAsync().ConfigureAwait(true);
                }
                else if (SampleRuntime.IsSmokeTestEnabled)
                {
                    await RunSmokeAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("ShownError", ex.Message));
                MessageBox.Show(this, ex.Message, "mpv sample", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 非同步初始化範例 runtime 與播放器控制項。
        /// </summary>
        /// <returns>初始化成功時為 <see langword="true"/>。</returns>
        private async Task<bool> InitializeRuntimeAsync()
        {
            SetStatusText("正在準備 runtime...");
            try
            {
                await Task.Run(async () => await SampleRuntime.InstallOrUpdateAsync().ConfigureAwait(false)).ConfigureAwait(true);
                SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, _player.PlayerOptions);
                ApplySelectedYtdlpFormatToPlayerOptions();
                _player.InitializePlayer();
                _runtimeReady = true;
                SetPlaybackControlsEnabled(true);
                SetStatusText("播放器已初始化");
                AppendEventLine(CreateLifecycleLine("RuntimeReady", SampleRuntime.RuntimeDirectory));
                return true;
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                SetStatusText("runtime 初始化失敗");
                AppendEventLine(CreateLifecycleLine("RuntimeError", ex.Message));
                if (SampleRuntime.IsSmokeTestEnabled || SampleRuntime.IsFeatureSmokeTestEnabled)
                {
                    SampleRuntime.WriteSmokeLine("WinFormsSample", "FAILED Runtime: " + ex.Message);
                    Close();
                    await Task.Delay(1000).ConfigureAwait(true);
                    Environment.Exit(Environment.ExitCode);
                }

                MessageBox.Show(this, ex.Message, "mpv runtime", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 執行 WinForms 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("WinFormsSample", () => _player.Player, Close);
        }

        /// <summary>
        /// 執行 WinForms 範例進階功能冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private async Task RunFeatureSmokeAsync()
        {
            try
            {
                await SampleRuntime.WaitForPlaybackAsync("WinFormsSample", () => _player.Player).ConfigureAwait(true);
                _features.ShowOsd();
                _features.SeekRelative(1);
                _features.ChangeVolume(-5);
                _features.ChangeVolume(5);
                _features.ToggleMute();
                _features.ToggleMute();
                _features.CycleSpeed();
                _features.CycleSpeed();
                _features.CycleSpeed();
                _features.AddSampleSubtitle();
                _features.DumpTracks();
                _features.LoadSampleConfig();
                await _features.LoadSampleLuaScriptAsync().ConfigureAwait(true);
                await _features.RunDenoDiagnosticsAsync().ConfigureAwait(true);
                _features.TakeScreenshot();
                Environment.ExitCode = 0;
                SampleRuntime.WriteSmokeLine("WinFormsSample", "FEATURES OK");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                SampleRuntime.WriteSmokeLine("WinFormsSample", "FAILED Features: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                Close();
                await Task.Delay(1000).ConfigureAwait(true);
                Environment.Exit(Environment.ExitCode);
            }
        }

        /// <summary>
        /// 處理播放器建立事件並開始輸出 libmpv 事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerCreated(object? sender, EventArgs e)
        {
            _eventBridge?.Dispose();
            if (_player.Player != null)
            {
                _currentPlayer = _player.Player;
                _eventBridge = new SamplePlayerEventBridge(_player.Player, AppendEventLine);
                _statusDispatcher.RequestUpdate();
            }
        }

        /// <summary>
        /// 處理載入按鈕點選事件並載入輸入的媒體來源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void LoadButtonClick(object? sender, EventArgs e)
        {
            LoadCurrentSource();
        }

        /// <summary>
        /// 切換目前播放器的暫停狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PauseButtonClick(object? sender, EventArgs e)
        {
            if (!EnsureRuntimeReady())
            {
                return;
            }

            if (_player.Player != null)
            {
                _eventBridge?.WriteLifecycle("Pause", "切換播放器暫停狀態。");
                _player.Player.Pause = !_player.Player.Pause;
            }
        }

        /// <summary>
        /// 停止目前播放項目。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void StopButtonClick(object? sender, EventArgs e)
        {
            if (!EnsureRuntimeReady())
            {
                return;
            }

            _eventBridge?.WriteLifecycle("Stop", "停止目前播放項目。");
            _player.Player?.Stop();
        }

        /// <summary>
        /// 在格式選項變更時套用 yt-dlp 格式。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void FormatComboBoxSelectedIndexChanged(object? sender, EventArgs e)
        {
            ComboBox? comboBox = sender as ComboBox;
            SampleYtdlpFormatChoice? choice = comboBox?.SelectedItem as SampleYtdlpFormatChoice;
            if (choice == null)
            {
                return;
            }

            try
            {
                ApplySelectedYtdlpFormatToPlayerOptions();
                if (_runtimeReady && _player.Player != null)
                {
                    _features.ApplyYtdlpFormat(choice);
                }
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FormatError", ex.Message));
            }
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            if (!EnsureRuntimeReady())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_urlTextBox.Text))
            {
                AppendEventLine(CreateLifecycleLine("LoadFileSkipped", "媒體來源不可為空白。"));
                return;
            }

            try
            {
                _eventBridge?.WriteLifecycle("LoadFile", _urlTextBox.Text);
                _player.LoadFile(_urlTextBox.Text, MpvLoadFileMode.Replace);
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("LoadFileError", ex.Message));
                MessageBox.Show(this, ex.Message, "mpv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 建立範例根版面。
        /// </summary>
        /// <returns>包含工具列、功能列、播放區域與事件清單的根版面。</returns>
        private Control CreateRootLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, SampleRuntime.SampleToolbarHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, SampleRuntime.SampleFeaturePanelHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, SampleRuntime.SampleEventLogHeight));
            root.Controls.Add(CreateToolbar(), 0, 0);
            root.Controls.Add(CreateFeaturePanel(), 0, 1);
            root.Controls.Add(CreatePlayerSurface(), 0, 2);
            root.Controls.Add(_eventListBox, 0, 3);
            return root;
        }

        /// <summary>
        /// 建立範例工具列。
        /// </summary>
        /// <returns>包含來源輸入與播放命令的工具列。</returns>
        private Control CreateToolbar()
        {
            TableLayoutPanel topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = SampleRuntime.SampleToolbarHeight,
                Padding = new Padding(SampleRuntime.SampleControlPadding),
                ColumnCount = 4,
                RowCount = 1
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SampleRuntime.SampleButtonWidth + SampleRuntime.SampleControlSpacing));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SampleRuntime.SampleButtonWidth + SampleRuntime.SampleControlSpacing));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SampleRuntime.SampleButtonWidth));
            topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, SampleRuntime.SampleButtonHeight));
            topPanel.Controls.Add(_urlTextBox, 0, 0);
            topPanel.Controls.Add(_loadButton, 1, 0);
            topPanel.Controls.Add(_pauseButton, 2, 0);
            topPanel.Controls.Add(_stopButton, 3, 0);
            return topPanel;
        }

        /// <summary>
        /// 建立進階功能展示列。
        /// </summary>
        /// <returns>包含格式、狀態與 API 按鈕的功能列。</returns>
        private Control CreateFeaturePanel()
        {
            FlowLayoutPanel panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(SampleRuntime.SampleControlPadding, 4, SampleRuntime.SampleControlPadding, 4),
                WrapContents = true,
                AutoScroll = false
            };

            panel.Controls.Add(_formatComboBox);
            panel.Controls.Add(_statusLabel);
            panel.Controls.Add(CreateFeatureButton("OSD", () => _features.ShowOsd()));
            panel.Controls.Add(CreateFeatureButton("-10s", () => _features.SeekRelative(-10)));
            panel.Controls.Add(CreateFeatureButton("+10s", () => _features.SeekRelative(10)));
            panel.Controls.Add(CreateFeatureButton("Vol-", () => _features.ChangeVolume(-5)));
            panel.Controls.Add(CreateFeatureButton("Vol+", () => _features.ChangeVolume(5)));
            panel.Controls.Add(CreateFeatureButton("Mute", () => _features.ToggleMute()));
            panel.Controls.Add(CreateFeatureButton("Speed", () => _features.CycleSpeed()));
            panel.Controls.Add(CreateFeatureButton("Sub", () => _features.AddSampleSubtitle()));
            panel.Controls.Add(CreateFeatureButton("Tracks", () => _features.DumpTracks()));
            panel.Controls.Add(CreateFeatureButton("Shot", () => _features.TakeScreenshot()));
            panel.Controls.Add(CreateFeatureButton("Config", () => _features.LoadSampleConfig()));
            panel.Controls.Add(CreateAsyncFeatureButton("Lua", () => _features.LoadSampleLuaScriptAsync()));
            panel.Controls.Add(CreateAsyncFeatureButton("yt-dlp", () => _features.RunYtdlpDiagnosticsAsync(_urlTextBox.Text)));
            panel.Controls.Add(CreateAsyncFeatureButton("Deno", () => _features.RunDenoDiagnosticsAsync()));
            panel.Controls.Add(CreateAsyncFeatureButton("FFmpeg", () => _features.RunFFmpegDiagnosticsAsync()));
            panel.Controls.Add(CreateAsyncFeatureButton("Update yt", () => _features.RunYtdlpSelfUpdateAsync(), SampleRuntime.SampleYtdlpUpdateButtonWidth));
            panel.Controls.Add(CreateAsyncFeatureButton("Update Deno", () => _features.RunDenoSelfUpgradeAsync(), SampleRuntime.SampleDenoUpdateButtonWidth));
            return panel;
        }

        /// <summary>
        /// 建立播放區域與覆蓋層展示。
        /// </summary>
        /// <returns>包含安全播放器與一般覆蓋層對照區域的面板。</returns>
        private Control CreatePlayerSurface()
        {
            TableLayoutPanel surface = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ColumnCount = 1,
                RowCount = 2
            };
            surface.RowStyles.Add(new RowStyle(SizeType.Absolute, SampleRuntime.SampleAirspaceComparisonHeight));
            surface.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            surface.Controls.Add(CreateHeaderPanel(), 0, 0);
            surface.Controls.Add(CreateVideoPanel(), 0, 1);
            return surface;
        }

        /// <summary>
        /// 建立左右對照標題列。
        /// </summary>
        /// <returns>包含安全覆蓋層與一般覆蓋層標題的面板。</returns>
        private static Control CreateHeaderPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            panel.Controls.Add(CreateHeaderBadge("WinForms HWND 播放區：控制項同層展示", ThemeColor(SampleTheme.AccentBadgeOpaqueArgb), new Padding(16, 6, 8, 6)), 0, 0);
            panel.Controls.Add(CreateHeaderBadge("WinForms Z-order 對照：一般 Label 覆蓋", ThemeColor(SampleTheme.ContrastBadgeOpaqueArgb), new Padding(8, 6, 16, 6)), 1, 0);
            return panel;
        }

        /// <summary>
        /// 建立播放視訊與安全覆蓋層面板。
        /// </summary>
        /// <returns>包含播放器、安全覆蓋層與一般覆蓋層對照標籤的播放面板。</returns>
        private Control CreateVideoPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            Label overlayLabel = new Label
            {
                AutoSize = false,
                Text = "WinForms HWND 控制項覆蓋",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = ThemeColor(SampleTheme.AccentBadgeOpaqueArgb),
                ForeColor = Color.White,
                Width = SampleRuntime.SampleOverlayBadgeWidth,
                Height = SampleRuntime.SampleOverlayBadgeHeight,
                Left = 16,
                Top = 16
            };

            Label normalOverlayLabel = new Label
            {
                AutoSize = false,
                Text = "WinForms Label Z-order 對照",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = ThemeColor(SampleTheme.ContrastBadgeOpaqueArgb),
                ForeColor = Color.White,
                Width = SampleRuntime.SampleOverlayBadgeWidth,
                Height = SampleRuntime.SampleOverlayBadgeHeight,
                Left = 16,
                Top = 16,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            panel.Controls.Add(overlayLabel);
            panel.Controls.Add(_player);
            panel.Controls.Add(normalOverlayLabel);
            panel.Resize += (sender, e) => PositionRightOverlay(panel, normalOverlayLabel);
            PositionRightOverlay(panel, normalOverlayLabel);
            overlayLabel.BringToFront();
            normalOverlayLabel.BringToFront();
            return panel;
        }

        /// <summary>
        /// 將一般覆蓋層標籤固定在播放面板右上角。
        /// </summary>
        /// <param name="panel">播放面板。</param>
        /// <param name="label">要定位的標籤。</param>
        private static void PositionRightOverlay(Panel panel, Label label)
        {
            label.Left = Math.Max(16, panel.ClientSize.Width - label.Width - 16);
        }

        /// <summary>
        /// 建立播放區對照標題列。
        /// </summary>
        /// <param name="text">要顯示的標籤文字。</param>
        /// <param name="backColor">標籤背景色彩。</param>
        /// <param name="margin">標籤外距。</param>
        /// <returns>已套用固定尺寸與色彩的對照標籤。</returns>
        private static Control CreateHeaderBadge(string text, Color backColor, Padding margin)
        {
            Label label = new Label
            {
                AutoSize = false,
                Text = text,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = backColor,
                ForeColor = Color.White,
                Dock = DockStyle.Fill,
                Margin = margin
            };
            return label;
        }

        /// <summary>
        /// 執行同步範例功能並處理錯誤。
        /// </summary>
        /// <param name="action">要執行的功能。</param>
        private void RunFeature(Action action)
        {
            if (!EnsureRuntimeReady())
            {
                return;
            }

            try
            {
                action();
                _statusDispatcher.RequestUpdate();
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FeatureError", ex.Message));
            }
        }

        /// <summary>
        /// 執行非同步範例功能並處理錯誤。
        /// </summary>
        /// <param name="action">要執行的非同步功能。</param>
        /// <returns>代表功能執行流程的工作。</returns>
        private async Task RunFeatureAsync(Func<Task> action)
        {
            if (!TryBeginAsyncFeature())
            {
                return;
            }

            try
            {
                await action().ConfigureAwait(true);
                _statusDispatcher.RequestUpdate();
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FeatureError", ex.Message));
            }
            finally
            {
                EndAsyncFeature();
            }
        }

        /// <summary>
        /// 確認 runtime 已完成初始化。
        /// </summary>
        /// <returns>runtime 已就緒時為 <see langword="true"/>。</returns>
        private bool EnsureRuntimeReady()
        {
            if (_runtimeReady)
            {
                return true;
            }

            AppendEventLine(CreateLifecycleLine("RuntimePending", "runtime 尚未初始化完成。"));
            return false;
        }

        /// <summary>
        /// 嘗試開始執行非同步功能。
        /// </summary>
        /// <returns>可執行非同步功能時為 <see langword="true"/>。</returns>
        private bool TryBeginAsyncFeature()
        {
            if (!EnsureRuntimeReady())
            {
                return false;
            }

            if (!_asyncFeatureGate.TryEnter())
            {
                AppendEventLine(CreateLifecycleLine("FeatureBusy", "已有非同步功能正在執行。"));
                return false;
            }

            SetFeatureButtonsEnabled(false);
            return true;
        }

        /// <summary>
        /// 結束非同步功能執行狀態。
        /// </summary>
        private void EndAsyncFeature()
        {
            _asyncFeatureGate.Exit();
            SetFeatureButtonsEnabled(_runtimeReady);
        }

        /// <summary>
        /// 將事件文字加入 UI 清單。
        /// </summary>
        /// <param name="line">要加入事件清單的文字列。</param>
        private void AppendEventLine(string line)
        {
            _eventLogDispatcher.Enqueue(line);
        }

        /// <summary>
        /// 批次加入事件文字列到 UI 清單。
        /// </summary>
        /// <param name="lines">要加入事件清單的文字列集合。</param>
        private void AppendEventLines(IReadOnlyList<string> lines)
        {
            if (IsDisposed || lines.Count == 0)
            {
                return;
            }

            _eventListBox.BeginUpdate();
            try
            {
                foreach (string line in lines)
                {
                    _eventListBox.Items.Add(line);
                }

                while (_eventListBox.Items.Count > EventLogLimit)
                {
                    _eventListBox.Items.RemoveAt(0);
                }
            }
            finally
            {
                _eventListBox.EndUpdate();
            }

            if (_eventListBox.Items.Count > 0)
            {
                _eventListBox.TopIndex = _eventListBox.Items.Count - 1;
            }
        }

        /// <summary>
        /// 將事件清單更新排入 WinForms UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的更新。</param>
        /// <returns>成功排入 UI 執行緒時為 <see langword="true"/>。</returns>
        private bool ScheduleEventLogFlush(Action action)
        {
            return ScheduleUiUpdate(action);
        }

        /// <summary>
        /// 將指定動作排入 WinForms UI 執行緒。
        /// </summary>
        /// <param name="action">要在 UI 執行緒執行的動作。</param>
        /// <returns>成功排入或直接執行動作時為 <see langword="true"/>。</returns>
        private bool ScheduleUiUpdate(Action action)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return false;
            }

            try
            {
                if (InvokeRequired)
                {
                    _ = BeginInvoke(action);
                    return true;
                }

                action();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// 設定播放與功能控制項是否可操作。
        /// </summary>
        /// <param name="enabled">控制項可操作時為 <see langword="true"/>。</param>
        private void SetPlaybackControlsEnabled(bool enabled)
        {
            SetButtonEnabled(_loadButton, enabled);
            SetButtonEnabled(_pauseButton, enabled);
            SetButtonEnabled(_stopButton, enabled);
            _formatComboBox.Enabled = enabled;
            SetFeatureButtonsEnabled(enabled && !_asyncFeatureGate.IsRunning);
        }

        /// <summary>
        /// 設定進階功能按鈕是否可操作。
        /// </summary>
        /// <param name="enabled">按鈕可操作時為 <see langword="true"/>。</param>
        private void SetFeatureButtonsEnabled(bool enabled)
        {
            foreach (Button button in _featureButtons)
            {
                SetButtonEnabled(button, enabled);
            }
        }

        /// <summary>
        /// 將目前選擇的 yt-dlp 格式套用到播放器選項。
        /// </summary>
        private void ApplySelectedYtdlpFormatToPlayerOptions()
        {
            SampleYtdlpFormatChoice? selectedChoice = _formatComboBox.SelectedItem as SampleYtdlpFormatChoice;
            if (selectedChoice != null)
            {
                SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, selectedChoice);
            }
        }

        /// <summary>
        /// 套用背景輪詢取得的狀態列文字。
        /// </summary>
        /// <param name="text">要顯示的狀態列文字。</param>
        private void SetStatusText(string text)
        {
            if (!IsDisposed)
            {
                _statusLabel.Text = text;
            }
        }

        /// <summary>
        /// 建立範例生命週期文字列。
        /// </summary>
        /// <param name="stage">生命週期階段名稱。</param>
        /// <param name="detail">階段補充內容。</param>
        /// <returns>可顯示在事件清單中的生命週期文字列。</returns>
        private static string CreateLifecycleLine(string stage, string detail)
        {
            return DateTimeOffset.Now.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + " [lifecycle] " + stage + " | " + detail;
        }

        /// <summary>
        /// 建立 yt-dlp 格式下拉選單。
        /// </summary>
        /// <returns>已填入格式選項的下拉選單。</returns>
        private ComboBox CreateFormatComboBox()
        {
            ComboBox comboBox = new ComboBox
            {
                Width = 132,
                Height = SampleRuntime.SampleButtonHeight,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 0, SampleRuntime.SampleControlSpacing, 0)
            };

            IReadOnlyList<SampleYtdlpFormatChoice> choices = SampleFeatureController.CreateYtdlpFormatChoices();
            SampleYtdlpFormatChoice defaultChoice = SampleFeatureController.CreateDefaultYtdlpFormatChoice();
            int selectedIndex = 0;
            foreach (SampleYtdlpFormatChoice choice in choices)
            {
                comboBox.Items.Add(choice);
                if (string.Equals(choice.Selector, defaultChoice.Selector, StringComparison.Ordinal))
                {
                    selectedIndex = comboBox.Items.Count - 1;
                }
            }

            comboBox.SelectedIndex = selectedIndex;
            if (comboBox.SelectedItem is SampleYtdlpFormatChoice selectedChoice)
            {
                SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, selectedChoice);
            }

            comboBox.SelectedIndexChanged += FormatComboBoxSelectedIndexChanged;
            return comboBox;
        }

        /// <summary>
        /// 建立標準尺寸的命令按鈕。
        /// </summary>
        /// <param name="text">要顯示在按鈕上的文字。</param>
        /// <returns>已套用範例標準尺寸的按鈕。</returns>
        private static Button CreateCommandButton(string text)
        {
            Button button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Width = SampleRuntime.SampleButtonWidth,
                Height = SampleRuntime.SampleButtonHeight,
                Margin = new Padding(0, 0, SampleRuntime.SampleControlSpacing, 0)
            };
            ApplyButtonStyle(button);
            return button;
        }

        /// <summary>
        /// 建立同步功能按鈕。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="action">點選時要執行的功能。</param>
        /// <returns>已建立的功能按鈕。</returns>
        private Button CreateFeatureButton(string text, Action action)
        {
            Button button = CreateFeatureButtonCore(text, SampleRuntime.SampleFeatureButtonWidth);
            button.Click += (sender, e) => RunFeature(action);
            return button;
        }

        /// <summary>
        /// 建立非同步功能按鈕。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="action">點選時要執行的非同步功能。</param>
        /// <param name="width">按鈕寬度。</param>
        /// <returns>已建立的功能按鈕。</returns>
        private Button CreateAsyncFeatureButton(string text, Func<Task> action, int width = SampleRuntime.SampleFeatureButtonWidth)
        {
            Button button = CreateFeatureButtonCore(text, width);
            button.Click += async (sender, e) => await RunFeatureAsync(action).ConfigureAwait(true);
            return button;
        }

        /// <summary>
        /// 建立功能按鈕共用外觀。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="width">按鈕寬度。</param>
        /// <returns>已套用共用外觀的按鈕。</returns>
        private Button CreateFeatureButtonCore(string text, int width)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = SampleRuntime.SampleButtonHeight,
                Margin = new Padding(0, 0, SampleRuntime.SampleControlSpacing, 4)
            };
            ApplyButtonStyle(button);
            _featureButtons.Add(button);
            return button;
        }

        /// <summary>
        /// 套用範例按鈕的固定深色外觀。
        /// </summary>
        /// <param name="button">要套用外觀的按鈕。</param>
        private static void ApplyButtonStyle(Button button)
        {
            button.UseVisualStyleBackColor = true;
        }

        /// <summary>
        /// 設定按鈕可用狀態。
        /// </summary>
        /// <param name="button">要設定的按鈕。</param>
        /// <param name="enabled">按鈕可操作時為 <see langword="true"/>。</param>
        private static void SetButtonEnabled(Button button, bool enabled)
        {
            button.Enabled = enabled;
        }

        /// <summary>
        /// 將 <see cref="SampleTheme"/> 的 ARGB 整數轉成 WinForms 顏色物件。
        /// </summary>
        /// <param name="argb">要轉換的 ARGB 整數。</param>
        /// <returns>對應的 WinForms 顏色物件。</returns>
        private static Color ThemeColor(int argb)
        {
            return Color.FromArgb(argb);
        }
    }
}
