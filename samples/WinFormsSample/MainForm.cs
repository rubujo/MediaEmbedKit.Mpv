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
        private const int EventLogLimit = 200;

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
        /// 週期性更新狀態列的計時器。
        /// </summary>
        private readonly Timer _statusTimer;
        /// <summary>
        /// 範例進階功能控制器。
        /// </summary>
        private readonly SampleFeatureController _features;
        /// <summary>
        /// 將播放器事件轉接到範例事件清單。
        /// </summary>
        private SamplePlayerEventBridge? _eventBridge;

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
                Dock = DockStyle.Fill
            };
            _player.PlayerCreated += PlayerCreated;
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, _player.PlayerOptions);

            _features = new SampleFeatureController(() => _player.Player, AppendEventLine);
            _formatComboBox = CreateFormatComboBox();
            _statusLabel = new Label
            {
                AutoSize = false,
                Width = 380,
                Height = SampleRuntime.SampleButtonHeight,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Gainsboro,
                BackColor = Color.FromArgb(28, 28, 28),
                Margin = new Padding(0, 0, SampleRuntime.SampleControlSpacing, 0)
            };

            _eventListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 22, 22),
                ForeColor = Color.Gainsboro,
                Font = new Font(FontFamily.GenericMonospace, 8.25f),
                HorizontalScrollbar = true,
                IntegralHeight = false
            };

            _statusTimer = new Timer
            {
                Interval = 1000
            };
            _statusTimer.Tick += StatusTimerTick;

            Controls.Add(CreateRootLayout());
            Shown += MainFormShown;
            _statusTimer.Start();
            AppendEventLine(CreateLifecycleLine("FormCreated", "範例視窗已建立，等待 WinForms Handle 建立播放器。"));
        }

        /// <summary>
        /// 釋放範例視窗所使用的受控與非受控資源。
        /// </summary>
        /// <param name="disposing">正在釋放受控資源時為 <see langword="true"/>。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusTimer.Stop();
                _statusTimer.Tick -= StatusTimerTick;
                _eventBridge?.WriteLifecycle("FormDispose", "視窗正在釋放，準備取消事件訂閱。");
                _eventBridge?.Dispose();
                _player.PlayerCreated -= PlayerCreated;
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
            AppendEventLine(CreateLifecycleLine("Shown", "視窗已顯示，準備載入預設媒體來源。"));
            LoadCurrentSource();
            if (SampleRuntime.IsSmokeTestEnabled)
            {
                await RunSmokeAsync().ConfigureAwait(true);
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
        /// 處理播放器建立事件並開始輸出 libmpv 事件。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PlayerCreated(object? sender, EventArgs e)
        {
            _eventBridge?.Dispose();
            if (_player.Player != null)
            {
                _eventBridge = new SamplePlayerEventBridge(_player.Player, AppendEventLine);
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
                SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, choice);
                if (_player.Player != null)
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
        /// 更新播放狀態列。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void StatusTimerTick(object? sender, EventArgs e)
        {
            _statusLabel.Text = _features.GetStatusText();
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
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
                BackColor = Color.FromArgb(18, 18, 18),
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
            panel.Controls.Add(CreateFeatureButton("Lua", () => _features.LoadSampleLuaScript()));
            panel.Controls.Add(CreateAsyncFeatureButton("yt-dlp", () => _features.RunYtdlpDiagnosticsAsync(_urlTextBox.Text)));
            panel.Controls.Add(CreateAsyncFeatureButton("Deno", () => _features.RunDenoDiagnosticsAsync()));
            panel.Controls.Add(CreateAsyncFeatureButton("Update yt", () => _features.RunYtdlpSelfUpdateAsync()));
            panel.Controls.Add(CreateAsyncFeatureButton("Update Deno", () => _features.RunDenoSelfUpgradeAsync()));
            return panel;
        }

        /// <summary>
        /// 建立播放區域與覆蓋層展示。
        /// </summary>
        /// <returns>包含播放器與 WinForms 覆蓋層的面板。</returns>
        private Control CreatePlayerSurface()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            Label overlayLabel = new Label
            {
                AutoSize = false,
                Text = "WinForms 同一 HWND 家族覆蓋層",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Width = 260,
                Height = 32,
                Left = 16,
                Top = 16
            };

            panel.Controls.Add(_player);
            panel.Controls.Add(overlayLabel);
            overlayLabel.BringToFront();
            return panel;
        }

        /// <summary>
        /// 執行同步範例功能並處理錯誤。
        /// </summary>
        /// <param name="action">要執行的功能。</param>
        private void RunFeature(Action action)
        {
            try
            {
                action();
                _statusLabel.Text = _features.GetStatusText();
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
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppendEventLine(CreateLifecycleLine("FeatureError", ex.Message));
            }
        }

        /// <summary>
        /// 將事件文字加入 UI 清單。
        /// </summary>
        /// <param name="line">要加入事件清單的文字列。</param>
        private void AppendEventLine(string line)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                _ = BeginInvoke(new Action<string>(AppendEventLine), line);
                return;
            }

            _eventListBox.Items.Add(line);
            while (_eventListBox.Items.Count > EventLogLimit)
            {
                _eventListBox.Items.RemoveAt(0);
            }

            _eventListBox.TopIndex = _eventListBox.Items.Count - 1;
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
            foreach (SampleYtdlpFormatChoice choice in choices)
            {
                comboBox.Items.Add(choice);
            }

            comboBox.SelectedIndex = 3;
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
            return new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Width = SampleRuntime.SampleButtonWidth,
                Height = SampleRuntime.SampleButtonHeight,
                Margin = new Padding(0, 0, SampleRuntime.SampleControlSpacing, 0)
            };
        }

        /// <summary>
        /// 建立同步功能按鈕。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="action">點選時要執行的功能。</param>
        /// <returns>已建立的功能按鈕。</returns>
        private Button CreateFeatureButton(string text, Action action)
        {
            Button button = CreateFeatureButtonCore(text);
            button.Click += (sender, e) => RunFeature(action);
            return button;
        }

        /// <summary>
        /// 建立非同步功能按鈕。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <param name="action">點選時要執行的非同步功能。</param>
        /// <returns>已建立的功能按鈕。</returns>
        private Button CreateAsyncFeatureButton(string text, Func<Task> action)
        {
            Button button = CreateFeatureButtonCore(text);
            button.Click += async (sender, e) => await RunFeatureAsync(action).ConfigureAwait(true);
            return button;
        }

        /// <summary>
        /// 建立功能按鈕共用外觀。
        /// </summary>
        /// <param name="text">按鈕文字。</param>
        /// <returns>已套用共用外觀的按鈕。</returns>
        private static Button CreateFeatureButtonCore(string text)
        {
            return new Button
            {
                Text = text,
                Width = 76,
                Height = SampleRuntime.SampleButtonHeight,
                Margin = new Padding(0, 0, SampleRuntime.SampleControlSpacing, 4)
            };
        }
    }
}
