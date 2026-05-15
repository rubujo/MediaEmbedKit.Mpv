# MediaEmbedKit.Mpv.SoakTests

> 連續播放 soak harness：循環 Load → 播放 → Stop → Dispose，每回合強制 full GC 後取樣，
> 跑滿目標時長後線性回歸 GC heap、Working Set、Handle Count 對時間的斜率，
> 推估 24 小時整段累計成長並比對門檻。

本專案 **不在 release gate 內**（24 小時太長）。實機驗證沒慢性 leak 才執行；
日常 leak 哨兵交給 [`MediaEmbedKit.Mpv.StressTests`](../MediaEmbedKit.Mpv.StressTests)
內的「播放器長時間建立／釋放記憶體 leak 檢查」（50 回合短跑）。

## 執行

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.SoakTests\MediaEmbedKit.Mpv.SoakTests.csproj --configuration Release -- --hours 24
```

短時間冒煙：

```powershell
dotnet run --project .\tests\MediaEmbedKit.Mpv.SoakTests\MediaEmbedKit.Mpv.SoakTests.csproj --configuration Release -- --hours 0.05 --playback-seconds 5 --idle-seconds 1
```

## 重要選項

| 選項 | 預設 | 說明 |
|---|---|---|
| `--hours` | 24 | 整段跑多久 |
| `--playback-seconds` | 60 | 每回合播放（wall clock）秒數 |
| `--idle-seconds` | 5 | 回合間 idle |
| `--output-dir` | `.tmp/soak-{時戳}/` | samples.csv + report.txt 輸出位置 |
| `--runtime-directory` | env / auto | 自帶 runtime 資料夾（含 libmpv-2.dll） |
| `--gc-heap-growth-mb` | 100 | GC heap 24h 累計成長門檻（MB） |
| `--working-set-growth-mb` | 300 | Working Set 24h 累計成長門檻（MB） |
| `--handle-growth` | 100 | Handle Count 24h 累計成長門檻 |
| `--warmup-iterations` | 10 | 回歸分析跳過前 n 回合作為 warmup |

## 取樣與分析

每回合：

1. 建立 `MpvPlayer`（`vo=null`、`ao=null`、`loop-file=inf`）
2. `Initialize()` → `LoadFile(soak-tone.wav)`
3. 監看 `time-pos`，wall clock 達 `--playback-seconds` 後 `Stop()`
4. 釋放 player → `GC.Collect → WaitForPendingFinalizers → GC.Collect`
5. 取樣 GC heap、Working Set、Private Memory、Handle Count
6. 寫一列到 `samples.csv`

分析（跑完或 Ctrl-C）：

- 跳過 `--warmup-iterations` 後做線性回歸
- 推算 24h 整段累計成長 = `slope_per_second * (24 * 3600)`
- 任一項超過門檻或有錯誤回合 → exit 1
- 全部過 → exit 0

`samples.csv` 欄位：

```
iteration,elapsed_seconds,gc_heap_bytes,working_set_bytes,private_memory_bytes,handle_count,playback_reached,observed_playback_seconds,error
```

## Ctrl-C

收到 SIGINT 會收尾目前進行中的回合（同一份 csv 已 flush），對已收樣本做分析後輸出
`report.txt` 才離開。
