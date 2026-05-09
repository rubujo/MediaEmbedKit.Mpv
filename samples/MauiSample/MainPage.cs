using System;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Maui;
using MediaEmbedKit.Mpv.Samples;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MediaEmbedKit.Mpv.Samples.Maui
{
    /// <summary>
    /// 表示 .NET MAUI 範例的主要頁面。
    /// </summary>
    public sealed class MainPage : ContentPage
    {
        /// <summary>
        /// 顯示 libmpv 視訊內容的 MAUI 檢視。
        /// </summary>
        private readonly MpvView _player;
        /// <summary>
        /// 讓使用者輸入檔案路徑或媒體網址的輸入方塊。
        /// </summary>
        private readonly Entry _sourceEntry;
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
        /// 表示預設媒體是否已載入。
        /// </summary>
        private bool _initialSourceLoaded;
        /// <summary>
        /// 表示冒煙測試是否已啟動。
        /// </summary>
        private bool _smokeStarted;

        /// <summary>
        /// 初始化 <see cref="MainPage"/> 類別的新執行個體。
        /// </summary>
        public MainPage()
        {
            Title = "MediaEmbedKit.Mpv MAUI Sample";
            BackgroundColor = Colors.Black;

            _sourceEntry = new Entry
            {
                Text = SampleRuntime.PlaybackUrl,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center
            };

            _player = new MpvView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            SampleRuntime.CopyTo(SampleRuntime.PlayerOptions, _player.PlayerOptions);

            _loadButton = CreateCommandButton("Load");
            _loadButton.Clicked += LoadButtonClicked;
            _pauseButton = CreateCommandButton("Pause");
            _pauseButton.Clicked += PauseButtonClicked;
            _stopButton = CreateCommandButton("Stop");
            _stopButton.Clicked += StopButtonClicked;

            Grid controls = new Grid
            {
                HeightRequest = SampleRuntime.SampleToolbarHeight,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth)),
                    new ColumnDefinition(new GridLength(SampleRuntime.SampleButtonWidth))
                },
                ColumnSpacing = SampleRuntime.SampleControlSpacing,
                Padding = SampleRuntime.SampleControlPadding,
                BackgroundColor = Color.FromArgb("#181818")
            };
            controls.Add(_sourceEntry, 0, 0);
            controls.Add(_loadButton, 1, 0);
            controls.Add(_pauseButton, 2, 0);
            controls.Add(_stopButton, 3, 0);

            Grid root = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Star)
                }
            };
            root.Add(controls, 0, 0);
            root.Add(_player, 0, 1);

            Content = root;
        }

        /// <summary>
        /// 在 MAUI 頁面顯示後載入預設媒體並執行冒煙測試。
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_initialSourceLoaded)
            {
                _initialSourceLoaded = true;
                LoadCurrentSource();
            }

            if (SampleRuntime.IsSmokeTestEnabled && !_smokeStarted)
            {
                _smokeStarted = true;
                await RunSmokeAsync().ConfigureAwait(true);
            }
        }

        /// <summary>
        /// 執行 MAUI Windows 範例播放冒煙測試。
        /// </summary>
        /// <returns>代表冒煙測試流程的工作。</returns>
        private Task RunSmokeAsync()
        {
            return SampleRuntime.RunSmokeUntilPlaybackAsync("MauiSample", () => _player.Player, CloseApplication);
        }

        /// <summary>
        /// 處理載入按鈕點選事件並載入輸入的媒體來源。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void LoadButtonClicked(object? sender, EventArgs e)
        {
            LoadCurrentSource();
        }

        /// <summary>
        /// 切換目前播放器的暫停狀態。
        /// </summary>
        /// <param name="sender">引發事件的物件。</param>
        /// <param name="e">事件資料。</param>
        private void PauseButtonClicked(object? sender, EventArgs e)
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
        private void StopButtonClicked(object? sender, EventArgs e)
        {
            _player.Player?.Stop();
        }

        /// <summary>
        /// 載入目前輸入的媒體來源。
        /// </summary>
        private void LoadCurrentSource()
        {
            string? source = _sourceEntry.Text;
            if (!string.IsNullOrWhiteSpace(source))
            {
                _player.LoadFile(source!);
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
                WidthRequest = SampleRuntime.SampleButtonWidth,
                HeightRequest = SampleRuntime.SampleButtonHeight,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center
            };
        }

        /// <summary>
        /// 關閉 MAUI 範例應用程式。
        /// </summary>
        private static void CloseApplication()
        {
            Application.Current?.Quit();
        }
    }
}
