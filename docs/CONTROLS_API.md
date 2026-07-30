# 控制項共通綁定 API

本文件描述 MediaEmbedKit.Mpv 5 套 UI 框架控制項共同提供的 bindable property 與 ICommand。所有屬性與指令在 WPF、Avalonia、WinUI 3、MAUI Windows 與 WinForms 上具有相同語意，且皆與 libmpv 屬性雙向同步（唯讀者單向）。

## 控制項對應

| 框架 | 控制項型別 |
| --- | --- |
| WinForms | `MediaEmbedKit.Mpv.WinForms.MpvPlayerControl` |
| WPF | `MediaEmbedKit.Mpv.Wpf.MpvWpfPlayer` |
| Avalonia | `MediaEmbedKit.Mpv.Avalonia.MpvAvaloniaPlayer` |
| WinUI 3 | `MediaEmbedKit.Mpv.WinUI.MpvWinUiPlayer` |
| MAUI Windows | `MediaEmbedKit.Mpv.Maui.Windows.MpvView` |

## 共通屬性

WinForms 透過 `INotifyPropertyChanged` 與 `Control.DataBindings` 支援程式碼資料繫結；WPF、Avalonia、WinUI 3 與 MAUI Windows 則使用各自的 property system。下表列出 5 套控制項共同提供的屬性。

| 屬性 | 型別 | 預設值 | 對應 mpv 屬性 | 唯讀 | 框架 | 說明 |
| --- | --- | --- | --- | --- | --- | --- |
| `Source` | `string?` | `null` | `loadfile` | 否 | 全部 | 變更時自動載入新媒體；空字串或 `null` 不觸發載入。 |
| `Position` | `TimeSpan` | `00:00:00` | `time-pos` | 否 | 全部 | 雙向繫結時設值會觸發 `seek` 到絕對位置。 |
| `Duration` | `TimeSpan` | `00:00:00` | `duration` | 是 | 全部 | 由 mpv 在 `FileLoaded` 後填入。 |
| `Volume` | `double` | `100.0` | `volume` | 否 | 全部 | 範圍 0–130。 |
| `IsPaused` | `bool` | `false` | `pause` | 否 | 全部 | |
| `IsMuted` | `bool` | `false` | `mute` | 否 | 全部 | |
| `PlaybackState` | `MpvPlaybackState` | `Idle` | n/a（事件聚合） | 是 | 全部 | 由 libmpv `StartFile`/`FileLoaded`/`EndFile`/`Idle`/`Shutdown` 聚合，詳見 `MpvPlaybackState`。 |
| `IsPlayerReady` | `bool` | `false` | n/a | 是 | 全部 | `Player` 已建立並完成初始化時為 `true`；可用於停用尚不可執行的 UI。 |
| `LastError` | `Exception?` | `null` | n/a | 是 | 全部 | 最近一次初始化、載入、命令、跳轉、屬性寫入或後端操作失敗。 |
| `PlaylistIndex` | `int` | `0` | `playlist-pos` | 否 | 全部 | 以 0 起始；設值會跳到該播放清單項目。負數不寫入 player。 |
| `Chapter` | `int?` | `null` | `chapter` | 否 | 全部 | 以 0 起始；`null` 代表無章節或尚未載入。mpv `-1` 自動映射為 `null`。設值為 `null` 或負數時不寫入 player。 |
| `OverlayContent` | `UIElement?` / `View?` / `WinUiElement?` | `null` | n/a | 否 | WPF / WinUI / MAUI | AirSpace 覆蓋層內容；HwndHost 之上的 WPF / WinUI / MAUI 元素，解決 mpv child HWND 蓋住 framework 內容的 z-order 問題。 |
| `IsOverlayOpen` | `bool` | `true` | n/a | 否 | WPF / WinUI / MAUI | 控制覆蓋層是否顯示。 |
| `OverlayView` | `View?` | `null` | n/a | 否 | MAUI | MAUI 專屬；以 MAUI `View` 為覆蓋層內容（與 `OverlayContent` 二擇一）。 |

Avalonia 沒有 child HWND airspace 問題，使用端用標準 Avalonia `Grid` / `Panel` 組合即可達成覆蓋效果，因此不提供 `OverlayContent` 屬性。

設計階段（VS Toolbox / Blend property grid / Avalonia DevTools）：可在設計工具設定的公開屬性均掛 `[Category("MediaEmbedKit.Mpv")]`；`Player` / `PlayerOptions` 與執行期播放狀態屬性掛 `[Browsable(false)]` 隱藏於 property grid，避免設計工具序列化瞬時播放狀態。

### 各框架實作型別

| 屬性類別 | WinForms | WPF | Avalonia | WinUI 3 | MAUI |
| --- | --- | --- | --- | --- | --- |
| 可讀寫 | INotifyPropertyChanged | `DependencyProperty` | `StyledProperty<T>` | `DependencyProperty` | `BindableProperty` |
| 唯讀 | INotifyPropertyChanged | `DependencyPropertyKey` | `DirectProperty<T,U>` | `DependencyProperty`（內部 SetValue 寫入） | `BindablePropertyKey` |

### 雙向綁定的循環防護

控制項以內部 `_suppressPlayerWrite` 旗標確保由 `player` 反射回來的值不會再寫回 `player`，避免循環抖動。setter / property-changed 回呼讀到旗標時直接 return。所有跨執行緒寫回都會 marshal 到 UI 執行緒（WPF `Dispatcher.BeginInvoke`、Avalonia `Dispatcher.UIThread.Post`、WinUI `DispatcherQueue.TryEnqueue`、MAUI `Dispatcher.Dispatch`、WinForms `Control.BeginInvoke`）。

### 執行緒模型

控制項已替您處理 libmpv 背景事件迴圈 → UI thread 的 marshalling，因此可以直接以一般 XAML / WinForms binding 使用上表所有屬性。

但如果您**自行訂閱** `MpvPlayer.EventReceived` / `PropertyChanged` / `StateChanged`（控制項以外的事件），這些 callback 仍然在 libmpv 背景執行緒觸發，必須自行 marshal 至 UI thread 才能修改 UI 元素。詳見 `docs/HIGH_LEVEL_API.md` 的「事件分派與執行緒模型」。

### 載入與錯誤處理

所有控制項都提供 `LoadAsync(MpvMediaItem, MpvLoadFileMode, TimeSpan?, CancellationToken)`，可等待 `FileLoaded` 並取得逾時、取消或載入錯誤。Avalonia、WinUI 3 與 MAUI Windows 應在 `IsPlayerReady` 為 `true` 或 `PlayerCreated` 事件後呼叫；WPF 與 WinForms 會視需要先完成初始化。

控制項不再靜默忽略播放操作錯誤。失敗時會更新唯讀 `LastError` 並引發 `OperationFailed`；事件引數的 `Operation` 可區分 `Initialize`、`Load`、`Command`、`Seek`、`PropertyWrite` 與 `Backend`，`Source` 則在可用時包含媒體來源。這些通知已切回 UI 執行緒。

## 共通 命令

所有控制項都提供下列 `System.Windows.Input.ICommand`，由 `MediaEmbedKit.Mpv.MpvRelayCommand` 包裝；`CanExecute` 與 player 生命週期同步，player 建立或釋放時會呼叫 `RaiseCanExecuteChanged`。

| 指令 | 行為 | 對應 mpv 操作 |
| --- | --- | --- |
| `PlayCommand` | `IsPaused = false` | `pause=no` |
| `PauseCommand` | `IsPaused = true` | `pause=yes` |
| `StopCommand` | 呼叫 `MpvPlayer.Stop()` | `stop` |
| `TogglePauseCommand` | 切換 `IsPaused` | `pause = !pause` |
| `ToggleMuteCommand` | 切換 `IsMuted` | `mute = !mute` |

## MVVM 範例

### WPF / WinUI / MAUI XAML

WPF 與 WinUI 將控制項 `DataContext` 指向 player 本體後即可：

```xml
<TextBlock Text="{Binding PlaybackState}" />
<Slider Minimum="0" Maximum="130"
        Value="{Binding Volume, Mode=TwoWay}" />
<Button Content="Toggle Pause"
        Command="{Binding TogglePauseCommand}" />
```

MAUI XAML 寫法相同；MAUI Windows 平台僅支援 `MpvView`。

### Avalonia（程式碼設定 binding）

```csharp
control.Bind(TextBlock.TextProperty,
    new Binding(nameof(MpvAvaloniaPlayer.PlaybackState))
    {
        Source = playerControl,
        StringFormat = "狀態 = {0}"
    });
```

### WinForms（INotifyPropertyChanged）

```csharp
stateLabel.DataBindings.Add(new Binding(
    nameof(Label.Text),
    playerControl,
    nameof(MpvPlayerControl.PlaybackState),
    formattingEnabled: true)
{
    FormatString = "狀態 = {0}"
});

muteButton.Click += (_, _) => playerControl.ToggleMuteCommand.Execute(null);
```

## 範例對照

5 個 sample 各自示範 MVVM 綁定區段：

| Sample | 示範元素 |
| --- | --- |
| `samples/WpfSample/MainWindow.xaml` | `DataContext = playerHost`、`PlaybackState` 文字、`Volume` 雙向、`TogglePauseCommand` / `ToggleMuteCommand` |
| `samples/WinUISample/MainWindow.xaml` | `MvvmDemoBar.DataContext = playerHost`、同上元素 |
| `samples/AvaloniaSample/MainWindow.axaml.cs` | `_mvvmStateTextBlock.Bind(...)` + `TogglePauseCommand` / `StopCommand` 取代 click 內容 |
| `samples/MauiSample/MainPage.xaml.cs` | `_mvvmStateLabel.SetBinding(...)` + Command click |
| `samples/WinFormsSample/MainForm.cs` | `_mvvmStateLabel.DataBindings.Add(...)` + Command click |

## 注意事項

- `MpvWinUiPlayer.Duration` 與 `PlaybackState` 在 WinUI 3 沒有 `RegisterReadOnly` 等效 API，因此公開為一般 DP；但 `DurationProperty` 與 `PlaybackStateProperty` 的 `PropertyChangedCallback` 會在偵測到非 player 來源寫入時立刻還原舊值，對呼叫端模擬唯讀語意。請勿透過 `Mode=TwoWay` binding 或 `SetValue` 寫入這兩個屬性。
- `MpvView.PlayerCreated` 在跨平台 handler 連線後才觸發；綁定到 `Player` 屬性必須在 `PlayerCreated` 後讀取。MAUI 平台目前僅 Windows 提供實際播放後端，其他 MAUI 目標的 handler 為 stub，呼叫播放相關 API 會擲回 `PlatformNotSupportedException`，詳見 `docs/SUPPORT_MATRIX.md`。
- `Position` 連續寫入會觸發大量 `seek`；建議搭配 `IsDragging` 一類旗標，只在使用者放開滑桿時寫值。
