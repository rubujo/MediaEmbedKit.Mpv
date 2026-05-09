using System;
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
            _stopButton.Margin = new Padding(0, 0, 0, 0);
            _stopButton.Click += StopButtonClick;

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

            _player = new MpvPlayerControl
            {
                Dock = DockStyle.Fill
            };
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, _player.PlayerOptions);

            Controls.Add(_player);
            Controls.Add(topPanel);
            Shown += MainFormShown;
        }

        /// <summary>
        /// 在視窗第一次顯示時載入預設播放範例。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void MainFormShown(object? sender, EventArgs e)
        {
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
            _player.Player?.Stop();
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            try
            {
                _player.LoadFile(_urlTextBox.Text, MpvLoadFileMode.Replace);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "mpv", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
    }
}
