#nullable disable

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MediaEmbedKit.Mpv.Samples.WinForms;

partial class MainForm
{
    /// <summary>
    /// 必要的設計工具變數。
    /// </summary>
    private IContainer components = null;

    /// <summary>
    /// 設計工具支援所需的方法 - 請勿使用程式碼編輯器修改這個方法的內容。
    /// </summary>
    /// <remarks>
    /// 21 個按鈕（Load/Pause/Stop + 18 個 feature）、4 個 TableLayoutPanel、
    /// FlowLayoutPanel、4 個 Label badge、ComboBox、ListBox 與 MpvPlayerControl 都
    /// 由設計工具序列化。Sample 端的動態邏輯（SampleFeatureController 建構、ComboBox
    /// 項目填入、DataBindings.Add、Dispatcher 串接、Resize 事件位置調整）由 MainForm
    /// 建構式在 InitializeComponent 後續執行。
    /// 數值常數對齊 SampleRuntime（SampleButtonWidth=88、SampleButtonHeight=32、
    /// SampleFeatureButtonWidth=76、SampleControlSpacing=8、SampleControlPadding=8、
    /// SampleToolbarHeight=48、SampleEventLogHeight=168、
    /// SampleAirspaceComparisonHeight=40、SampleOverlayBadgeWidth=340、
    /// SampleOverlayBadgeHeight=32、SampleYtdlpUpdateButtonWidth=88、
    /// SampleDenoUpdateButtonWidth=120、SampleWindowWidth=1200、SampleWindowHeight=720）。
    /// </remarks>
    private void InitializeComponent()
    {
        this._rootLayout = new TableLayoutPanel();
        this._toolbarPanel = new TableLayoutPanel();
        this._featurePanel = new FlowLayoutPanel();
        this._playerSurfacePanel = new TableLayoutPanel();
        this._playerHeaderPanel = new TableLayoutPanel();
        this._playerVideoPanel = new Panel();
        this._safeHeaderLabel = new Label();
        this._normalHeaderLabel = new Label();
        this._safeOverlayLabel = new Label();
        this._normalOverlayLabel = new Label();
        this._urlTextBox = new TextBox();
        this._loadButton = new Button();
        this._pauseButton = new Button();
        this._stopButton = new Button();
        this._formatComboBox = new ComboBox();
        this._statusLabel = new Label();
        this._mvvmStateLabel = new Label();
        this._osdButton = new Button();
        this._seekBackwardButton = new Button();
        this._seekForwardButton = new Button();
        this._volumeDownButton = new Button();
        this._volumeUpButton = new Button();
        this._muteButton = new Button();
        this._speedButton = new Button();
        this._subtitleButton = new Button();
        this._tracksButton = new Button();
        this._screenshotButton = new Button();
        this._configButton = new Button();
        this._luaButton = new Button();
        this._ytdlpButton = new Button();
        this._denoButton = new Button();
        this._ffmpegButton = new Button();
        this._saveMp4Button = new Button();
        this._ytdlpUpdateButton = new Button();
        this._denoUpdateButton = new Button();
        this._eventListBox = new ListBox();
        this._player = new MediaEmbedKit.Mpv.WinForms.MpvPlayerControl();
        this._rootLayout.SuspendLayout();
        this._toolbarPanel.SuspendLayout();
        this._featurePanel.SuspendLayout();
        this._playerSurfacePanel.SuspendLayout();
        this._playerHeaderPanel.SuspendLayout();
        this._playerVideoPanel.SuspendLayout();
        this.SuspendLayout();
        //
        // _rootLayout
        //
        this._rootLayout.ColumnCount = 1;
        this._rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this._rootLayout.Dock = DockStyle.Fill;
        this._rootLayout.Name = "_rootLayout";
        this._rootLayout.RowCount = 4;
        this._rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        this._rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        this._rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this._rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
        this._rootLayout.Controls.Add(this._toolbarPanel, 0, 0);
        this._rootLayout.Controls.Add(this._featurePanel, 0, 1);
        this._rootLayout.Controls.Add(this._playerSurfacePanel, 0, 2);
        this._rootLayout.Controls.Add(this._eventListBox, 0, 3);
        //
        // _toolbarPanel
        //
        this._toolbarPanel.ColumnCount = 4;
        this._toolbarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this._toolbarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        this._toolbarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        this._toolbarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
        this._toolbarPanel.Dock = DockStyle.Top;
        this._toolbarPanel.Height = 48;
        this._toolbarPanel.Name = "_toolbarPanel";
        this._toolbarPanel.Padding = new Padding(8);
        this._toolbarPanel.RowCount = 1;
        this._toolbarPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        this._toolbarPanel.Controls.Add(this._urlTextBox, 0, 0);
        this._toolbarPanel.Controls.Add(this._loadButton, 1, 0);
        this._toolbarPanel.Controls.Add(this._pauseButton, 2, 0);
        this._toolbarPanel.Controls.Add(this._stopButton, 3, 0);
        //
        // _urlTextBox
        //
        this._urlTextBox.AutoSize = false;
        this._urlTextBox.Dock = DockStyle.Fill;
        this._urlTextBox.Height = 32;
        this._urlTextBox.Margin = new Padding(0, 2, 8, 0);
        this._urlTextBox.Name = "_urlTextBox";
        this._urlTextBox.TabIndex = 0;
        //
        // _loadButton
        //
        this._loadButton.Dock = DockStyle.Fill;
        this._loadButton.Height = 32;
        this._loadButton.Margin = new Padding(0, 0, 8, 0);
        this._loadButton.Name = "_loadButton";
        this._loadButton.TabIndex = 1;
        this._loadButton.Text = "Load";
        this._loadButton.UseVisualStyleBackColor = true;
        this._loadButton.Width = 88;
        this._loadButton.Click += new System.EventHandler(this.LoadButtonClick);
        //
        // _pauseButton
        //
        this._pauseButton.Dock = DockStyle.Fill;
        this._pauseButton.Height = 32;
        this._pauseButton.Margin = new Padding(0, 0, 8, 0);
        this._pauseButton.Name = "_pauseButton";
        this._pauseButton.TabIndex = 2;
        this._pauseButton.Text = "Pause";
        this._pauseButton.UseVisualStyleBackColor = true;
        this._pauseButton.Width = 88;
        this._pauseButton.Click += new System.EventHandler(this.PauseButtonClick);
        //
        // _stopButton
        //
        this._stopButton.Dock = DockStyle.Fill;
        this._stopButton.Height = 32;
        this._stopButton.Margin = new Padding(0);
        this._stopButton.Name = "_stopButton";
        this._stopButton.TabIndex = 3;
        this._stopButton.Text = "Stop";
        this._stopButton.UseVisualStyleBackColor = true;
        this._stopButton.Width = 88;
        this._stopButton.Click += new System.EventHandler(this.StopButtonClick);
        //
        // _featurePanel
        //
        this._featurePanel.AutoScroll = false;
        this._featurePanel.AutoSize = true;
        this._featurePanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this._featurePanel.Dock = DockStyle.Top;
        this._featurePanel.Name = "_featurePanel";
        this._featurePanel.Padding = new Padding(8, 4, 8, 4);
        this._featurePanel.WrapContents = true;
        this._featurePanel.Controls.Add(this._formatComboBox);
        this._featurePanel.Controls.Add(this._statusLabel);
        this._featurePanel.Controls.Add(this._mvvmStateLabel);
        this._featurePanel.Controls.Add(this._osdButton);
        this._featurePanel.Controls.Add(this._seekBackwardButton);
        this._featurePanel.Controls.Add(this._seekForwardButton);
        this._featurePanel.Controls.Add(this._volumeDownButton);
        this._featurePanel.Controls.Add(this._volumeUpButton);
        this._featurePanel.Controls.Add(this._muteButton);
        this._featurePanel.Controls.Add(this._speedButton);
        this._featurePanel.Controls.Add(this._subtitleButton);
        this._featurePanel.Controls.Add(this._tracksButton);
        this._featurePanel.Controls.Add(this._screenshotButton);
        this._featurePanel.Controls.Add(this._configButton);
        this._featurePanel.Controls.Add(this._luaButton);
        this._featurePanel.Controls.Add(this._ytdlpButton);
        this._featurePanel.Controls.Add(this._denoButton);
        this._featurePanel.Controls.Add(this._ffmpegButton);
        this._featurePanel.Controls.Add(this._saveMp4Button);
        this._featurePanel.Controls.Add(this._ytdlpUpdateButton);
        this._featurePanel.Controls.Add(this._denoUpdateButton);
        //
        // _formatComboBox
        //
        this._formatComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        this._formatComboBox.Height = 32;
        this._formatComboBox.Margin = new Padding(0, 0, 8, 0);
        this._formatComboBox.Name = "_formatComboBox";
        this._formatComboBox.Width = 132;
        this._formatComboBox.SelectedIndexChanged += new System.EventHandler(this.FormatComboBoxSelectedIndexChanged);
        //
        // _statusLabel
        //
        this._statusLabel.AutoSize = false;
        this._statusLabel.Height = 32;
        this._statusLabel.Margin = new Padding(0, 0, 8, 0);
        this._statusLabel.Name = "_statusLabel";
        this._statusLabel.Text = "播放器尚未初始化";
        this._statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        this._statusLabel.Width = 380;
        //
        // _mvvmStateLabel
        //
        this._mvvmStateLabel.AutoSize = false;
        this._mvvmStateLabel.Height = 32;
        this._mvvmStateLabel.Margin = new Padding(0, 0, 8, 0);
        this._mvvmStateLabel.Name = "_mvvmStateLabel";
        this._mvvmStateLabel.Text = "MVVM 綁定示範：狀態 = Idle";
        this._mvvmStateLabel.TextAlign = ContentAlignment.MiddleLeft;
        this._mvvmStateLabel.Width = 220;
        //
        // _osdButton
        //
        this._osdButton.Height = 32;
        this._osdButton.Margin = new Padding(0, 0, 8, 4);
        this._osdButton.Name = "_osdButton";
        this._osdButton.Text = "OSD";
        this._osdButton.UseVisualStyleBackColor = true;
        this._osdButton.Width = 76;
        this._osdButton.Click += new System.EventHandler(this.OsdClick);
        //
        // _seekBackwardButton
        //
        this._seekBackwardButton.Height = 32;
        this._seekBackwardButton.Margin = new Padding(0, 0, 8, 4);
        this._seekBackwardButton.Name = "_seekBackwardButton";
        this._seekBackwardButton.Text = "-10s";
        this._seekBackwardButton.UseVisualStyleBackColor = true;
        this._seekBackwardButton.Width = 76;
        this._seekBackwardButton.Click += new System.EventHandler(this.SeekBackwardClick);
        //
        // _seekForwardButton
        //
        this._seekForwardButton.Height = 32;
        this._seekForwardButton.Margin = new Padding(0, 0, 8, 4);
        this._seekForwardButton.Name = "_seekForwardButton";
        this._seekForwardButton.Text = "+10s";
        this._seekForwardButton.UseVisualStyleBackColor = true;
        this._seekForwardButton.Width = 76;
        this._seekForwardButton.Click += new System.EventHandler(this.SeekForwardClick);
        //
        // _volumeDownButton
        //
        this._volumeDownButton.Height = 32;
        this._volumeDownButton.Margin = new Padding(0, 0, 8, 4);
        this._volumeDownButton.Name = "_volumeDownButton";
        this._volumeDownButton.Text = "Vol-";
        this._volumeDownButton.UseVisualStyleBackColor = true;
        this._volumeDownButton.Width = 76;
        this._volumeDownButton.Click += new System.EventHandler(this.VolumeDownClick);
        //
        // _volumeUpButton
        //
        this._volumeUpButton.Height = 32;
        this._volumeUpButton.Margin = new Padding(0, 0, 8, 4);
        this._volumeUpButton.Name = "_volumeUpButton";
        this._volumeUpButton.Text = "Vol+";
        this._volumeUpButton.UseVisualStyleBackColor = true;
        this._volumeUpButton.Width = 76;
        this._volumeUpButton.Click += new System.EventHandler(this.VolumeUpClick);
        //
        // _muteButton
        //
        this._muteButton.Height = 32;
        this._muteButton.Margin = new Padding(0, 0, 8, 4);
        this._muteButton.Name = "_muteButton";
        this._muteButton.Text = "Mute";
        this._muteButton.UseVisualStyleBackColor = true;
        this._muteButton.Width = 76;
        this._muteButton.Click += new System.EventHandler(this.MuteClick);
        //
        // _speedButton
        //
        this._speedButton.Height = 32;
        this._speedButton.Margin = new Padding(0, 0, 8, 4);
        this._speedButton.Name = "_speedButton";
        this._speedButton.Text = "Speed";
        this._speedButton.UseVisualStyleBackColor = true;
        this._speedButton.Width = 76;
        this._speedButton.Click += new System.EventHandler(this.SpeedClick);
        //
        // _subtitleButton
        //
        this._subtitleButton.Height = 32;
        this._subtitleButton.Margin = new Padding(0, 0, 8, 4);
        this._subtitleButton.Name = "_subtitleButton";
        this._subtitleButton.Text = "Sub";
        this._subtitleButton.UseVisualStyleBackColor = true;
        this._subtitleButton.Width = 76;
        this._subtitleButton.Click += new System.EventHandler(this.SubtitleClick);
        //
        // _tracksButton
        //
        this._tracksButton.Height = 32;
        this._tracksButton.Margin = new Padding(0, 0, 8, 4);
        this._tracksButton.Name = "_tracksButton";
        this._tracksButton.Text = "Tracks";
        this._tracksButton.UseVisualStyleBackColor = true;
        this._tracksButton.Width = 76;
        this._tracksButton.Click += new System.EventHandler(this.TracksClick);
        //
        // _screenshotButton
        //
        this._screenshotButton.Height = 32;
        this._screenshotButton.Margin = new Padding(0, 0, 8, 4);
        this._screenshotButton.Name = "_screenshotButton";
        this._screenshotButton.Text = "Shot";
        this._screenshotButton.UseVisualStyleBackColor = true;
        this._screenshotButton.Width = 76;
        this._screenshotButton.Click += new System.EventHandler(this.ScreenshotClick);
        //
        // _configButton
        //
        this._configButton.Height = 32;
        this._configButton.Margin = new Padding(0, 0, 8, 4);
        this._configButton.Name = "_configButton";
        this._configButton.Text = "Config";
        this._configButton.UseVisualStyleBackColor = true;
        this._configButton.Width = 76;
        this._configButton.Click += new System.EventHandler(this.ConfigClick);
        //
        // _luaButton
        //
        this._luaButton.Height = 32;
        this._luaButton.Margin = new Padding(0, 0, 8, 4);
        this._luaButton.Name = "_luaButton";
        this._luaButton.Text = "Lua";
        this._luaButton.UseVisualStyleBackColor = true;
        this._luaButton.Width = 76;
        this._luaButton.Click += new System.EventHandler(this.LuaClick);
        //
        // _ytdlpButton
        //
        this._ytdlpButton.Height = 32;
        this._ytdlpButton.Margin = new Padding(0, 0, 8, 4);
        this._ytdlpButton.Name = "_ytdlpButton";
        this._ytdlpButton.Text = "yt-dlp";
        this._ytdlpButton.UseVisualStyleBackColor = true;
        this._ytdlpButton.Width = 76;
        this._ytdlpButton.Click += new System.EventHandler(this.YtdlpClick);
        //
        // _denoButton
        //
        this._denoButton.Height = 32;
        this._denoButton.Margin = new Padding(0, 0, 8, 4);
        this._denoButton.Name = "_denoButton";
        this._denoButton.Text = "Deno";
        this._denoButton.UseVisualStyleBackColor = true;
        this._denoButton.Width = 76;
        this._denoButton.Click += new System.EventHandler(this.DenoClick);
        //
        // _ffmpegButton
        //
        this._ffmpegButton.Height = 32;
        this._ffmpegButton.Margin = new Padding(0, 0, 8, 4);
        this._ffmpegButton.Name = "_ffmpegButton";
        this._ffmpegButton.Text = "FFmpeg";
        this._ffmpegButton.UseVisualStyleBackColor = true;
        this._ffmpegButton.Width = 76;
        this._ffmpegButton.Click += new System.EventHandler(this.FFmpegClick);
        //
        // _saveMp4Button
        //
        this._saveMp4Button.Height = 32;
        this._saveMp4Button.Margin = new Padding(0, 0, 8, 4);
        this._saveMp4Button.Name = "_saveMp4Button";
        this._saveMp4Button.Text = "Save MP4";
        this._saveMp4Button.UseVisualStyleBackColor = true;
        this._saveMp4Button.Width = 88;
        this._saveMp4Button.Click += new System.EventHandler(this.SaveMp4Click);
        //
        // _ytdlpUpdateButton
        //
        this._ytdlpUpdateButton.Height = 32;
        this._ytdlpUpdateButton.Margin = new Padding(0, 0, 8, 4);
        this._ytdlpUpdateButton.Name = "_ytdlpUpdateButton";
        this._ytdlpUpdateButton.Text = "Update yt";
        this._ytdlpUpdateButton.UseVisualStyleBackColor = true;
        this._ytdlpUpdateButton.Width = 88;
        this._ytdlpUpdateButton.Click += new System.EventHandler(this.YtdlpUpdateClick);
        //
        // _denoUpdateButton
        //
        this._denoUpdateButton.Height = 32;
        this._denoUpdateButton.Margin = new Padding(0, 0, 8, 4);
        this._denoUpdateButton.Name = "_denoUpdateButton";
        this._denoUpdateButton.Text = "Update Deno";
        this._denoUpdateButton.UseVisualStyleBackColor = true;
        this._denoUpdateButton.Width = 120;
        this._denoUpdateButton.Click += new System.EventHandler(this.DenoUpdateClick);
        //
        // _playerSurfacePanel
        //
        this._playerSurfacePanel.BackColor = Color.Black;
        this._playerSurfacePanel.ColumnCount = 1;
        this._playerSurfacePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this._playerSurfacePanel.Dock = DockStyle.Fill;
        this._playerSurfacePanel.Name = "_playerSurfacePanel";
        this._playerSurfacePanel.RowCount = 2;
        this._playerSurfacePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        this._playerSurfacePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this._playerSurfacePanel.Controls.Add(this._playerHeaderPanel, 0, 0);
        this._playerSurfacePanel.Controls.Add(this._playerVideoPanel, 0, 1);
        //
        // _playerHeaderPanel
        //
        this._playerHeaderPanel.ColumnCount = 2;
        this._playerHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        this._playerHeaderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        this._playerHeaderPanel.Dock = DockStyle.Fill;
        this._playerHeaderPanel.Name = "_playerHeaderPanel";
        this._playerHeaderPanel.RowCount = 1;
        this._playerHeaderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        this._playerHeaderPanel.Controls.Add(this._safeHeaderLabel, 0, 0);
        this._playerHeaderPanel.Controls.Add(this._normalHeaderLabel, 1, 0);
        //
        // _safeHeaderLabel
        //
        this._safeHeaderLabel.AutoSize = false;
        this._safeHeaderLabel.BackColor = Color.FromArgb(unchecked((int)0xFF0078D4));
        this._safeHeaderLabel.Dock = DockStyle.Fill;
        this._safeHeaderLabel.ForeColor = Color.White;
        this._safeHeaderLabel.Margin = new Padding(16, 6, 8, 6);
        this._safeHeaderLabel.Name = "_safeHeaderLabel";
        this._safeHeaderLabel.Text = "WinForms HWND 播放區：控制項同層展示";
        this._safeHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _normalHeaderLabel
        //
        this._normalHeaderLabel.AutoSize = false;
        this._normalHeaderLabel.BackColor = Color.FromArgb(unchecked((int)0xFF323130));
        this._normalHeaderLabel.Dock = DockStyle.Fill;
        this._normalHeaderLabel.ForeColor = Color.White;
        this._normalHeaderLabel.Margin = new Padding(8, 6, 16, 6);
        this._normalHeaderLabel.Name = "_normalHeaderLabel";
        this._normalHeaderLabel.Text = "WinForms Z-order 對照：一般 Label 會被影片覆蓋";
        this._normalHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // _playerVideoPanel
        //
        this._playerVideoPanel.BackColor = Color.Black;
        this._playerVideoPanel.Dock = DockStyle.Fill;
        this._playerVideoPanel.Name = "_playerVideoPanel";
        this._playerVideoPanel.Controls.Add(this._safeOverlayLabel);
        this._playerVideoPanel.Controls.Add(this._player);
        this._playerVideoPanel.Controls.Add(this._normalOverlayLabel);
        this._playerVideoPanel.Resize += new System.EventHandler(this.PlayerVideoPanelResize);
        //
        // _safeOverlayLabel
        //
        this._safeOverlayLabel.AutoSize = false;
        this._safeOverlayLabel.BackColor = Color.FromArgb(unchecked((int)0xFF0078D4));
        this._safeOverlayLabel.ForeColor = Color.White;
        this._safeOverlayLabel.Height = 32;
        this._safeOverlayLabel.Left = 16;
        this._safeOverlayLabel.Name = "_safeOverlayLabel";
        this._safeOverlayLabel.Text = "WinForms HWND 控制項覆蓋";
        this._safeOverlayLabel.TextAlign = ContentAlignment.MiddleCenter;
        this._safeOverlayLabel.Top = 16;
        this._safeOverlayLabel.Width = 340;
        //
        // _normalOverlayLabel
        //
        this._normalOverlayLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this._normalOverlayLabel.AutoSize = false;
        this._normalOverlayLabel.BackColor = Color.FromArgb(unchecked((int)0xFF323130));
        this._normalOverlayLabel.ForeColor = Color.White;
        this._normalOverlayLabel.Height = 32;
        this._normalOverlayLabel.Left = 16;
        this._normalOverlayLabel.Name = "_normalOverlayLabel";
        this._normalOverlayLabel.Text = "WinForms Label Z-order 對照（會被影片覆蓋）";
        this._normalOverlayLabel.TextAlign = ContentAlignment.MiddleCenter;
        this._normalOverlayLabel.Top = 16;
        this._normalOverlayLabel.Width = 340;
        //
        // _player
        //
        this._player.AutoInitialize = false;
        this._player.Dock = DockStyle.Fill;
        this._player.Name = "_player";
        this._player.TabIndex = 4;
        this._player.PlayerCreated += new System.EventHandler(this.PlayerCreated);
        //
        // _eventListBox
        //
        this._eventListBox.Dock = DockStyle.Fill;
        this._eventListBox.Font = new Font(FontFamily.GenericMonospace, 8.25F);
        this._eventListBox.HorizontalScrollbar = true;
        this._eventListBox.IntegralHeight = false;
        this._eventListBox.Name = "_eventListBox";
        this._eventListBox.TabIndex = 5;
        //
        // MainForm
        //
        this.AutoScaleDimensions = new SizeF(96F, 96F);
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.ClientSize = new Size(1200, 720);
        this.Controls.Add(this._rootLayout);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "MediaEmbedKit.Mpv WinForms Sample";
        this.Shown += new System.EventHandler(this.MainFormShown);
        this._playerVideoPanel.ResumeLayout(false);
        this._playerHeaderPanel.ResumeLayout(false);
        this._playerSurfacePanel.ResumeLayout(false);
        this._featurePanel.ResumeLayout(false);
        this._toolbarPanel.ResumeLayout(false);
        this._toolbarPanel.PerformLayout();
        this._rootLayout.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    private MediaEmbedKit.Mpv.WinForms.MpvPlayerControl _player;
    private TableLayoutPanel _rootLayout;
    private TableLayoutPanel _toolbarPanel;
    private FlowLayoutPanel _featurePanel;
    private TableLayoutPanel _playerSurfacePanel;
    private TableLayoutPanel _playerHeaderPanel;
    private Panel _playerVideoPanel;
    private Label _safeHeaderLabel;
    private Label _normalHeaderLabel;
    private Label _safeOverlayLabel;
    private Label _normalOverlayLabel;
    private TextBox _urlTextBox;
    private Button _loadButton;
    private Button _pauseButton;
    private Button _stopButton;
    private ComboBox _formatComboBox;
    private Label _statusLabel;
    private Label _mvvmStateLabel;
    private Button _osdButton;
    private Button _seekBackwardButton;
    private Button _seekForwardButton;
    private Button _volumeDownButton;
    private Button _volumeUpButton;
    private Button _muteButton;
    private Button _speedButton;
    private Button _subtitleButton;
    private Button _tracksButton;
    private Button _screenshotButton;
    private Button _configButton;
    private Button _luaButton;
    private Button _ytdlpButton;
    private Button _denoButton;
    private Button _ffmpegButton;
    private Button _saveMp4Button;
    private Button _ytdlpUpdateButton;
    private Button _denoUpdateButton;
    private ListBox _eventListBox;
}
