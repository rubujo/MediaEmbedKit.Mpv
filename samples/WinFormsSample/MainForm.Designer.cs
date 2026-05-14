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
    /// 動態 layout（CreateRootLayout / CreateToolbar / CreateFeaturePanel 等）、factory
    /// method 建出的按鈕（CreateCommandButton / CreateFormatComboBox）、含 lambda 的
    /// SampleFeatureController / SampleEventLogDispatcher / SampleStatusUpdateDispatcher
    /// 構造、以及 DataBindings.Add(...) 仍由 MainForm 建構式在 InitializeComponent 後續執行；
    /// 這些邏輯結構上無法被 WinForms designer 序列化，所以保留在 MainForm.cs。
    /// </remarks>
    private void InitializeComponent()
    {
        this._player = new MediaEmbedKit.Mpv.WinForms.MpvPlayerControl();
        this._urlTextBox = new TextBox();
        this._statusLabel = new Label();
        this._mvvmStateLabel = new Label();
        this._eventListBox = new ListBox();
        this.SuspendLayout();
        //
        // _player
        //
        this._player.AutoInitialize = false;
        this._player.Dock = DockStyle.Fill;
        this._player.Name = "_player";
        this._player.TabIndex = 0;
        this._player.PlayerCreated += new System.EventHandler(this.PlayerCreated);
        //
        // _urlTextBox
        //
        this._urlTextBox.AutoSize = false;
        this._urlTextBox.Dock = DockStyle.Fill;
        this._urlTextBox.Name = "_urlTextBox";
        this._urlTextBox.TabIndex = 1;
        //
        // _statusLabel
        //
        this._statusLabel.AutoSize = false;
        this._statusLabel.Name = "_statusLabel";
        this._statusLabel.TabIndex = 2;
        this._statusLabel.Text = "播放器尚未初始化";
        this._statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _mvvmStateLabel
        //
        this._mvvmStateLabel.AutoSize = false;
        this._mvvmStateLabel.Name = "_mvvmStateLabel";
        this._mvvmStateLabel.TabIndex = 3;
        this._mvvmStateLabel.Text = "MVVM 綁定示範：狀態 = Idle";
        this._mvvmStateLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // _eventListBox
        //
        this._eventListBox.Dock = DockStyle.Fill;
        this._eventListBox.HorizontalScrollbar = true;
        this._eventListBox.IntegralHeight = false;
        this._eventListBox.Name = "_eventListBox";
        this._eventListBox.TabIndex = 4;
        //
        // MainForm
        //
        this.AutoScaleDimensions = new SizeF(96F, 96F);
        this.AutoScaleMode = AutoScaleMode.Dpi;
        this.ClientSize = new Size(1200, 720);
        this.Controls.Add(this._player);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "MediaEmbedKit.Mpv WinForms Sample";
        this.Shown += new System.EventHandler(this.MainFormShown);
        this.ResumeLayout(false);
    }

    private MediaEmbedKit.Mpv.WinForms.MpvPlayerControl _player;
    private TextBox _urlTextBox;
    private Label _statusLabel;
    private Label _mvvmStateLabel;
    private ListBox _eventListBox;
}
