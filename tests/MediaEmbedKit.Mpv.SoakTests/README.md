# MediaEmbedKit.Mpv.SoakTests

> 連續播放 soak harness：以三路混合工作負載（WAV / MP4 / cancel-in-flight）循環
> Load → 播放 → Stop → Dispose，每回合強制 full GC 後取樣，跑滿目標時長後
> 同時做線性回歸與 Mann-Kendall 趨勢檢定，判斷有無慢性記憶體 / handle 洩漏。

本專案 **不在 release gate 內**（24 小時太長）。實機驗證沒慢性 leak 才執行；
日常 leak 哨兵交給 [`MediaEmbedKit.Mpv.StressTests`](../MediaEmbedKit.Mpv.StressTests)
內的「播放器長時間建立／釋放記憶體 leak 檢查」（50 回合短跑）。

## 覆蓋路徑

三路工作負載按 iteration mod 3 輪流（若找不到 ffmpeg.exe 則退化為 WAV / Cancel 兩路）：

| Workload | 媒體 | 覆蓋路徑 |
|---|---|---|
| `wav` | 啟動時生成的 90 秒 sine WAV（PCM s16le 44.1 kHz mono） | 純 audio decode |
| `mp4` | 啟動時 ffmpeg 產生的 5 秒 h264 + aac MP4（lavfi testsrc + sine） | video decode（libavcodec h264）+ audio decode（aac）+ mp4 demux |
| `cancel` | 與當前 workload 同檔 | LoadFile 後立即 Stop，cancel-in-flight 路徑 |

每回合都會：

- subscribe `WatchProperty<double>("time-pos")` + `WatchProperty<bool>("pause")` + dispose
- 在播放過程每秒讀 `duration` / `audio-codec` / `video-codec` / `pause` 一次，
  覆蓋 property cache / event 通路

仍以 `vo=null` + `ao=null` 跑，不開視窗、不出聲。**已知不覆蓋路徑**：VO (gpu / gpu-next)、
真實 AO (wasapi)、hwdec、subtitle renderer、yt-dlp 子行程、網路 stream。

## 執行

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.SoakTests\MediaEmbedKit.Mpv.SoakTests.csproj --configuration Release -- --hours 24
```

短時間冒煙：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.SoakTests\MediaEmbedKit.Mpv.SoakTests.csproj --configuration Release -- --hours 0.05 --playback-seconds 5 --idle-seconds 1 --runtime-directory .\.tmp\gui-playback-runtime
```

## 重要選項

| 選項 | 預設 | 說明 |
|---|---|---|
| `--hours` | 24 | 整段跑多久 |
| `--playback-seconds` | 60 | 每回合 wav / mp4 播放（wall clock）秒數；cancel 不適用 |
| `--idle-seconds` | 5 | 回合間 idle |
| `--output-dir` | `.tmp/soak-{時戳}/` | samples.csv + report.txt 輸出位置 |
| `--runtime-directory` | env / auto | 自帶 runtime 資料夾（含 libmpv-2.dll 與 ffmpeg.exe） |
| `--gc-heap-growth-mb` | 100 | GC heap 整段累計成長門檻（MB） |
| `--working-set-growth-mb` | 300 | Working Set 整段累計成長門檻（MB） |
| `--handle-growth` | 100 | Handle Count 整段累計成長門檻 |
| `--warmup-iterations` | 10 | 回歸 / MK 分析跳過前 n 回合作為 warmup |

## 取樣與分析

每回合（任一 workload 結束後）：

1. 釋放 player → `GC.Collect → WaitForPendingFinalizers → GC.Collect`
2. 取樣 GC heap、Working Set、Private Memory、Handle Count
3. 寫一列到 `samples.csv`

跑完或 Ctrl-C：

- 跳過 `--warmup-iterations` 個 warmup 樣本
- 對 GC heap / Working Set / Private Memory / Handle Count 各做：
  - **線性回歸**（best-fit slope per second） → 推算整段累計成長 vs 門檻
  - **Mann-Kendall 趨勢檢定**（無母數，抗 noise） → |Z| > 1.96 即 α=0.05 顯著趨勢；
    輸出「上升 / 平穩 / 下降」描述
- **MK 純資訊用，不參與 pass/fail**（業界做法：MK 對 magnitude 無感，1 byte/iter 累積也判顯著，
  實際 gate 仍以絕對量門檻為準）
- 任一項超過絕對量門檻或有錯誤回合 → exit 1
- 全部過 → exit 0；`report.txt` 含完整數字

`samples.csv` 欄位：

```
iteration,elapsed_seconds,workload,gc_heap_bytes,working_set_bytes,private_memory_bytes,handle_count,playback_reached,observed_playback_seconds,error
```

## Ctrl-C

收到 SIGINT 會收尾目前進行中的回合（同一份 csv 已 flush），對已收樣本做分析後輸出
`report.txt` 才離開。
