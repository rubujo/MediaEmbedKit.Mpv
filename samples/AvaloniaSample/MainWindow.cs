using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MediaEmbedKit.Mpv;
using MediaEmbedKit.Mpv.Avalonia;
using MediaEmbedKit.Mpv.Samples;

namespace MediaEmbedKit.Mpv.Samples.Avalonia
{
    /// <summary>
    /// 表示 Avalonia 範例的主要視窗。
    /// </summary>
    public sealed class MainWindow : Window
    {
        /// <summary>
        /// 顯示 libmpv 視訊內容的 Avalonia OpenGL 預覽控制項。
        /// </summary>
        private readonly MpvAvaloniaPlayer _player;
        /// <summary>
        /// 讓使用者輸入檔案路徑或媒體網址的文字方塊。
        /// </summary>
        private readonly TextBox _sourceBox;
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
        /// 表示預設媒體載入是否已排程。
        /// </summary>
        private bool _playbackStarted;
        /// <summary>
        /// 表示冒煙測試是否已啟動。
        /// </summary>
        private bool _smokeStarted;

        /// <summary>
        /// 初始化 <see cref="MainWindow"/> 類別的新執行個體。
        /// </summary>
        public MainWindow()
        {
            Title = "MediaEmbedKit.Mpv Avalonia Sample";
            Width = SampleRuntime.SampleWindowWidth;
            Height = SampleRuntime.SampleWindowHeight;
            MinWidth = SampleRuntime.SampleWindowWidth;
            MinHeight = SampleRuntime.SampleWindowHeight;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.Black;

            _sourceBox = new TextBox
            {
                Text = SampleRuntime.PlaybackUrl,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinHeight = SampleRuntime.SampleButtonHeight,
                Margin = new Thickness(0, 0, SampleRuntime.SampleControlSpacing, 0)
            };

            _loadButton = CreateCommandButton("Load");
            _loadButton.Click += OnLoadClicked;
            _pauseButton = CreateCommandButton("Pause");
            _pauseButton.Click += OnPauseClicked;
            _stopButton = CreateCommandButton("Stop");
            _stopButton.Margin = new Thickness(0);
            _stopButton.Click += OnStopClicked;

            _player = new MpvAvaloniaPlayer
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, _player.PlayerOptions);

            Content = CreateLayout();
            Opened += WindowOpened;
        }

        /// <summary>
        /// 在視窗開啟後載入預設媒體並執行冒煙測試。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private async void WindowOpened(object? sender, EventArgs e)
        {
            StartPlayback();
            if (SampleRuntime.IsSmokeTestEnabled && !_smokeStarted)
            {
                _smokeStarted = true;
                await RunSmokeAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// 處理載入按鈕點選事件並載入輸入的媒體來源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnLoadClicked(object? sender, RoutedEventArgs e)
        {
            LoadCurrentSource();
        }

        /// <summary>
        /// 處理暫停按鈕點選事件並切換播放器暫停狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnPauseClicked(object? sender, RoutedEventArgs e)
        {
            if (_player.Player != null)
            {
                _player.Player.Pause = !_player.Player.Pause;
            }
        }

        /// <summary>
        /// 處理停止按鈕點選事件並停止目前播放項目。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void OnStopClicked(object? sender, RoutedEventArgs e)
        {
            _player.Player?.Stop();
        }

        /// <summary>
        /// 載入預設媒體來源。
        /// </summary>
        private void StartPlayback()
        {
            if (_playbackStarted)
            {
                return;
            }

            _playbackStarted = true;
            LoadCurrentSource();
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            if (!string.IsNullOrWhiteSpace(_sourceBox.Text))
            {
                _player.LoadFile(_sourceBox.Text, MpvLoadFileMode.Replace);
            }
        }

        /// <summary>
        /// 執行 Avalonia 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("AvaloniaSample", () => _player.Player, Close);
        }

        /// <summary>
        /// 建立範例視窗版面。
        /// </summary>
        /// <returns>包含工具列與播放區域的根版面。</returns>
        private Grid CreateLayout()
        {
            Grid root = new Grid
            {
                Background = Brushes.Black,
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(SampleRuntime.SampleToolbarHeight, GridUnitType.Pixel)),
                    new RowDefinition(new GridLength(1, GridUnitType.Star))
                }
            };

            Grid toolbar = new Grid
            {
                Margin = new Thickness(SampleRuntime.SampleControlPadding),
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleControlSpacing, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleControlSpacing, GridUnitType.Pixel)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth, GridUnitType.Pixel))
                }
            };

            toolbar.Children.Add(_sourceBox);
            Grid.SetColumn(_loadButton, 1);
            toolbar.Children.Add(_loadButton);
            Grid.SetColumn(_pauseButton, 3);
            toolbar.Children.Add(_pauseButton);
            Grid.SetColumn(_stopButton, 5);
            toolbar.Children.Add(_stopButton);

            root.Children.Add(toolbar);
            Grid.SetRow(_player, 1);
            root.Children.Add(_player);
            return root;
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
                Content = text,
                MinWidth = SampleRuntime.SampleButtonWidth,
                MinHeight = SampleRuntime.SampleButtonHeight,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }
    }
}
