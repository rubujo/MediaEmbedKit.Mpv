using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Samples;
using MediaEmbedKit.Mpv.WinForms;

namespace MediaEmbedKit.Mpv.Samples.WinForms;

/// <summary>
/// 表示 WinForms 範例的主要視窗。layout 結構、所有控制項實例化、屬性設定與事件繫結
/// 由 <c>MainForm.Designer.cs</c> 序列化（對齊 VS 2026 designer 產出）。本檔僅處理
/// designer 無法序列化的部分：runtime 初始化、dispatcher / event bridge 串接、命名
/// click handler 對 <see cref="RunFeature"/> / <see cref="RunFeatureAsync"/> wrapper 的轉送、
/// ComboBox 動態項目填入、DataBindings 設定與 Resize 時對右側對照覆蓋層的位置調整。
/// </summary>
public sealed partial class MainForm : Form
{
    /// <summary>
    /// 範例事件輸出的最大保留列數。
    /// </summary>
    private const int EventLogLimit = 60;

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
    private readonly List<Button> _featureButtons;
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
        InitializeComponent();

        _urlTextBox.Text = SampleRuntime.PlaybackUrl;

        _featureButtons = new List<Button>
        {
            _osdButton,
            _seekBackwardButton,
            _seekForwardButton,
            _volumeDownButton,
            _volumeUpButton,
            _muteButton,
            _speedButton,
            _subtitleButton,
            _tracksButton,
            _screenshotButton,
            _configButton,
            _luaButton,
            _ytdlpButton,
            _denoButton,
            _ffmpegButton,
            _saveMp4Button,
            _ytdlpUpdateButton,
            _denoUpdateButton
        };

        _features = new SampleFeatureController(() => _currentPlayer, AppendEventLine);
        PopulateFormatComboBox();

        _mvvmStateLabel.DataBindings.Add(new Binding(nameof(Label.Text), _player, nameof(MpvPlayerControl.PlaybackState), formattingEnabled: true)
        {
            FormatString = "MVVM 綁定示範：狀態 = {0}"
        });

        _eventLogDispatcher = new SampleEventLogDispatcher(AppendEventLines, ScheduleEventLogFlush);
        _statusDispatcher = new SampleStatusUpdateDispatcher(() => _features.GetStatusText(), SetStatusText, ScheduleUiUpdate);

        SetPlaybackControlsEnabled(false);
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
            components?.Dispose();
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
            await _features.RunYtdlpDiagnosticsAsync(_urlTextBox.Text).ConfigureAwait(true);
            await _features.RunDenoDiagnosticsAsync().ConfigureAwait(true);
            await _features.RunFFmpegDiagnosticsAsync().ConfigureAwait(true);
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
    /// 載入按鈕點選事件。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">事件資料。</param>
    private void LoadButtonClick(object? sender, EventArgs e)
    {
        LoadCurrentSource();
    }

    /// <summary>
    /// 暫停按鈕點選事件：切換目前播放器的暫停狀態。
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
            _eventBridge?.WriteLifecycle("Pause", "透過 TogglePauseCommand 切換暫停狀態。");
            _player.TogglePauseCommand.Execute(null);
        }
    }

    /// <summary>
    /// 停止按鈕點選事件。
    /// </summary>
    /// <param name="sender">引發事件的物件。</param>
    /// <param name="e">事件資料。</param>
    private void StopButtonClick(object? sender, EventArgs e)
    {
        if (!EnsureRuntimeReady())
        {
            return;
        }

        _eventBridge?.WriteLifecycle("Stop", "透過 StopCommand 停止播放。");
        _player.StopCommand.Execute(null);
    }

    /// <summary>OSD 按鈕點選事件。</summary>
    private void OsdClick(object? sender, EventArgs e) => RunFeature(() => _features.ShowOsd());

    /// <summary>後退 10 秒按鈕點選事件。</summary>
    private void SeekBackwardClick(object? sender, EventArgs e) => RunFeature(() => _features.SeekRelative(-10));

    /// <summary>前進 10 秒按鈕點選事件。</summary>
    private void SeekForwardClick(object? sender, EventArgs e) => RunFeature(() => _features.SeekRelative(10));

    /// <summary>音量下降按鈕點選事件。</summary>
    private void VolumeDownClick(object? sender, EventArgs e) => RunFeature(() => _features.ChangeVolume(-5));

    /// <summary>音量上升按鈕點選事件。</summary>
    private void VolumeUpClick(object? sender, EventArgs e) => RunFeature(() => _features.ChangeVolume(5));

    /// <summary>靜音按鈕點選事件。</summary>
    private void MuteClick(object? sender, EventArgs e) => RunFeature(() => _features.ToggleMute());

    /// <summary>播放速度切換按鈕點選事件。</summary>
    private void SpeedClick(object? sender, EventArgs e) => RunFeature(() => _features.CycleSpeed());

    /// <summary>字幕載入按鈕點選事件。</summary>
    private void SubtitleClick(object? sender, EventArgs e) => RunFeature(() => _features.AddSampleSubtitle());

    /// <summary>軌道輸出按鈕點選事件。</summary>
    private void TracksClick(object? sender, EventArgs e) => RunFeature(() => _features.DumpTracks());

    /// <summary>截圖按鈕點選事件。</summary>
    private void ScreenshotClick(object? sender, EventArgs e) => RunFeature(() => _features.TakeScreenshot());

    /// <summary>設定載入按鈕點選事件。</summary>
    private void ConfigClick(object? sender, EventArgs e) => RunFeature(() => _features.LoadSampleConfig());

    /// <summary>Lua script 載入按鈕點選事件。</summary>
    private async void LuaClick(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.LoadSampleLuaScriptAsync()).ConfigureAwait(true);

    /// <summary>yt-dlp 診斷按鈕點選事件。</summary>
    private async void YtdlpClick(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunYtdlpDiagnosticsAsync(_urlTextBox.Text)).ConfigureAwait(true);

    /// <summary>Deno 診斷按鈕點選事件。</summary>
    private async void DenoClick(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunDenoDiagnosticsAsync()).ConfigureAwait(true);

    /// <summary>FFmpeg 診斷按鈕點選事件。</summary>
    private async void FFmpegClick(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunFFmpegDiagnosticsAsync()).ConfigureAwait(true);

    /// <summary>Save MP4 按鈕點選事件。</summary>
    private async void SaveMp4Click(object? sender, EventArgs e) => await RunFeatureAsync(() => EncodeCurrentSourceToMp4Async()).ConfigureAwait(true);

    /// <summary>yt-dlp 自我更新按鈕點選事件。</summary>
    private async void YtdlpUpdateClick(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunYtdlpSelfUpdateAsync()).ConfigureAwait(true);

    /// <summary>Deno 自我升級按鈕點選事件。</summary>
    private async void DenoUpdateClick(object? sender, EventArgs e) => await RunFeatureAsync(() => _features.RunDenoSelfUpgradeAsync()).ConfigureAwait(true);

    /// <summary>
    /// 在播放區尺寸變化時把右側 Z-order 對照覆蓋層維持在右上角。
    /// </summary>
    /// <param name="sender">引發事件的播放區面板。</param>
    /// <param name="e">事件資料。</param>
    private void PlayerVideoPanelResize(object? sender, EventArgs e)
    {
        if (sender is Panel panel)
        {
            _normalOverlayLabel.Left = Math.Max(16, panel.ClientSize.Width - _normalOverlayLabel.Width - 16);
        }
    }

    /// <summary>
    /// 以共用 <see cref="SampleEncodingHelper"/> 把當前 URL 來源前 5 秒轉碼成 mp4。
    /// </summary>
    /// <returns>代表編碼流程的工作。</returns>
    private async Task EncodeCurrentSourceToMp4Async()
    {
        if (!EnsureRuntimeReady())
        {
            return;
        }

        string source = _urlTextBox.Text;
        if (string.IsNullOrWhiteSpace(source))
        {
            AppendEventLine(CreateLifecycleLine("Encode", "請先在 URL 欄輸入來源。"));
            return;
        }

        MpvPlayerOptions playerOptions = new MpvPlayerOptions();
        SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, playerOptions);
        SampleYtdlpFormatChoice? selectedChoice = _formatComboBox.SelectedItem as SampleYtdlpFormatChoice;
        if (selectedChoice != null)
        {
            SampleFeatureController.ApplyYtdlpFormat(playerOptions, selectedChoice);
        }

        try
        {
            await SampleEncodingHelper.EncodeFirstFiveSecondsToMp4Async(
                source,
                playerOptions,
                line => AppendEventLine(CreateLifecycleLine("Encode", line))).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            AppendEventLine(CreateLifecycleLine("EncodeError", ex.GetType().Name + ": " + ex.Message));
        }
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

        ScrollEventListToEnd();
    }

    /// <summary>
    /// 把事件清單捲動到底端：算出當前可視 item 數，把 <see cref="ListBox.TopIndex"/>
    /// 設成讓最後一個 item 對齊在 viewport 底端的索引。直接設 <c>TopIndex = Count - 1</c>
    /// 會把最後一個 item 放在 viewport 頂端、下方留空，視覺上像沒捲動。
    /// </summary>
    private void ScrollEventListToEnd()
    {
        int itemCount = _eventListBox.Items.Count;
        if (itemCount <= 0)
        {
            return;
        }

        int itemHeight = Math.Max(1, _eventListBox.ItemHeight);
        int visibleItems = Math.Max(1, _eventListBox.ClientSize.Height / itemHeight);
        _eventListBox.TopIndex = Math.Max(0, itemCount - visibleItems);
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
        _loadButton.Enabled = enabled;
        _pauseButton.Enabled = enabled;
        _stopButton.Enabled = enabled;
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
            button.Enabled = enabled;
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
    /// 填入 yt-dlp 格式下拉選單的選項並選擇預設值。
    /// </summary>
    private void PopulateFormatComboBox()
    {
        IReadOnlyList<SampleYtdlpFormatChoice> choices = SampleFeatureController.CreateYtdlpFormatChoices();
        SampleYtdlpFormatChoice defaultChoice = SampleFeatureController.CreateDefaultYtdlpFormatChoice();
        int selectedIndex = 0;
        foreach (SampleYtdlpFormatChoice choice in choices)
        {
            _formatComboBox.Items.Add(choice);
            if (string.Equals(choice.Selector, defaultChoice.Selector, StringComparison.Ordinal))
            {
                selectedIndex = _formatComboBox.Items.Count - 1;
            }
        }

        _formatComboBox.SelectedIndex = selectedIndex;
        if (_formatComboBox.SelectedItem is SampleYtdlpFormatChoice selectedChoice)
        {
            SampleFeatureController.ApplyYtdlpFormat(_player.PlayerOptions, selectedChoice);
        }
    }
}
