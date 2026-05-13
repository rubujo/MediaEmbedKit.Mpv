using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Downloads;

namespace MediaEmbedKit.Mpv.Tests
{
    /// <summary>
    /// 執行不需要原生 libmpv 的核心 API 驗證。
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 測試執行進入點。
        /// </summary>
        /// <param name="args">命令列引數；目前未使用。</param>
        /// <returns>所有測試通過時傳回 0，否則傳回 1。</returns>
        private static async Task<int> Main(string[] args)
        {
            _ = args;
            TestRunner runner = new TestRunner();
            runner.Add("yt-dlp 格式預設值對應", VerifyYtdlpFormatPresets);
            runner.Add("yt-dlp 格式參數驗證", VerifyYtdlpFormatValidation);
            runner.Add("mpv encoding mode 選項", VerifyEncodingOptions);
            runner.Add("播放器選項 fluent helper", VerifyPlayerOptionFluentHelpers);
            runner.Add("外部工具命令列引數格式化", VerifyExternalToolArgumentFormatting);
            runner.Add("native asset digest 強制驗證", VerifyNativeAssetDigestValidation);
            runner.Add("native asset 釘選 SHA-256 驗證", VerifyNativeAssetPinnedSha256Validation);
            runner.Add("native asset checksum 解析", VerifyNativeAssetChecksumParsing);
            runner.Add("native asset 來源鎖定驗證", VerifyNativeAssetSourceLockValidation);
            runner.Add("runtime 下載驗證策略預設值", VerifyRuntimeVerificationOptionDefaults);
            runner.Add("Windows runtime FFmpeg 選項預設值", VerifyWindowsRuntimeFFmpegOptionDefaults);
            runner.Add("播放器選項預設值", VerifyPlayerOptionDefaults);
            runner.Add("執行階段來源 catalog 收斂", VerifyRuntimeCatalogs);
            runner.Add("未知平台安裝不觸發下載", VerifyUnknownPlatformInstallAsync);
            runner.Add("Windows 執行階段播放器選項", VerifyWindowsRuntimePlayerOptions);
            runner.Add("MpvCapabilities 查詢與防呆", VerifyMpvCapabilities);
            runner.Add("MpvMediaItem 建構 per-file options", VerifyMpvMediaItemBuildFileOptions);
            runner.Add("MpvRuntimeHealthCheck 缺檔資料夾報告", VerifyMpvRuntimeHealthCheckMissingFiles);
            runner.Add("MpvLibraryUpdateScheduler 路徑與列舉", VerifyMpvLibraryUpdateSchedulerLayout);

            await runner.RunAsync().ConfigureAwait(false);
            return runner.FailedCount == 0 ? 0 : 1;
        }

        /// <summary>
        /// 驗證 <see cref="MpvRuntimeHealthCheck.AnalyzeAsync"/> 在缺檔資料夾會列出對應錯誤。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static async Task VerifyMpvRuntimeHealthCheckMissingFiles()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "mediaembedkit-health-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            try
            {
                MpvRuntimeHealthReport report = await MpvRuntimeHealthCheck.AnalyzeAsync(tempDirectory).ConfigureAwait(false);
                AssertEx.True(!report.IsLibMpvPresent, "空資料夾不應視為包含 libmpv-2.dll");
                AssertEx.True(!report.IsHealthy, "缺 libmpv 時不應視為健康");
                AssertEx.True(!report.IsYtdlpPresent, "空資料夾不應視為包含 yt-dlp.exe");
                AssertEx.True(!report.IsFFmpegPresent, "空資料夾不應視為包含 ffmpeg.exe");
                AssertEx.True(report.Errors.Count > 0, "缺檔報告應至少包含一筆錯誤");
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        /// <summary>
        /// 驗證 <see cref="MpvLibraryUpdateScheduler"/> 的路徑公開與暫存列舉。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyMpvLibraryUpdateSchedulerLayout()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "mediaembedkit-scheduler-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            try
            {
                MpvLibraryUpdateScheduler scheduler = new MpvLibraryUpdateScheduler(tempDirectory);
                AssertEx.Equal(Path.Combine(tempDirectory, "libmpv-2.dll"), scheduler.CurrentLibraryPath, "目前 libmpv 路徑");
                AssertEx.Equal(Path.Combine(tempDirectory, ".previous", "libmpv-2.dll"), scheduler.PreviousLibraryPath, "前一版 libmpv 路徑");
                AssertEx.Equal(Path.Combine(tempDirectory, ".updates"), scheduler.StagedRootDirectory, "暫存資料夾路徑");
                AssertEx.Equal(0, scheduler.ListStagedUpdates().Count, "空 .updates 資料夾應傳回空集合");

                string stagedDirectory = Path.Combine(scheduler.StagedRootDirectory, "20260513000000");
                Directory.CreateDirectory(stagedDirectory);
                File.WriteAllBytes(Path.Combine(stagedDirectory, "libmpv-2.dll"), new byte[] { 0xCA, 0xFE });
                System.Collections.Generic.IReadOnlyList<MpvLibraryStagedUpdate> staged = scheduler.ListStagedUpdates();
                AssertEx.Equal(1, staged.Count, "應辨識出單一暫存版本");
                AssertEx.Equal(stagedDirectory, staged[0].StagedDirectory, "暫存資料夾完整路徑");
                AssertEx.Equal(Path.Combine(stagedDirectory, "libmpv-2.dll"), staged[0].LibraryPath, "暫存 libmpv 完整路徑");

                System.Collections.Generic.IReadOnlyList<MpvLibraryStagedUpdate> pruned = scheduler.PruneStagedUpdates();
                AssertEx.Equal(1, pruned.Count, "PruneStagedUpdates 應傳回被清除的集合");
                AssertEx.True(!Directory.Exists(stagedDirectory), "清除後暫存資料夾應被刪除");
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證 <see cref="MpvMediaItem.BuildFileOptions"/> 會正確產生 mpv per-file options 字典。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyMpvMediaItemBuildFileOptions()
        {
            MpvMediaItem empty = new MpvMediaItem("https://example.com/stream");
            IDictionary<string, string> emptyOptions = empty.BuildFileOptions();
            AssertEx.Equal(0, emptyOptions.Count, "預設媒體項目不應產生任何 per-file 選項");

            MpvMediaItem populated = new MpvMediaItem("https://example.com/stream")
            {
                StartTime = TimeSpan.FromSeconds(12.5),
                EndTime = TimeSpan.FromMinutes(2),
                YtdlpFormatPreset = MpvYtdlpFormatPreset.UpTo720p
            };
            populated.Headers["User-Agent"] = "Mozilla/5.0";
            populated.Headers["Referer"] = "https://example.com/page";
            populated.Options["hwdec"] = "auto-safe";

            IDictionary<string, string> options = populated.BuildFileOptions();
            AssertEx.Equal("12.5", options["start"], "起始時間應以秒數表示");
            AssertEx.Equal("120", options["end"], "結束時間應以秒數表示");
            AssertEx.Equal("auto-safe", options["hwdec"], "額外 mpv 選項應原樣套用");
            AssertEx.Equal("bestvideo*[height<=720]+bestaudio/best[height<=720]", options["ytdl-format"], "yt-dlp preset 應展開為 selector");
            AssertEx.True(options["http-header-fields"].Contains("User-Agent: Mozilla/5.0"), "HTTP 標頭應以 mpv 格式串接");
            AssertEx.True(options["http-header-fields"].Contains("Referer: https://example.com/page"), "HTTP 標頭應包含全部欄位");

            MpvMediaItem customFormat = new MpvMediaItem("file.mp4")
            {
                YtdlpFormat = "bestvideo+bestaudio"
            };
            AssertEx.Equal("bestvideo+bestaudio", customFormat.BuildFileOptions()["ytdl-format"], "顯式 yt-dlp 格式優先於 preset");

            AssertEx.Throws<ArgumentException>(
                delegate
                {
                    MpvMediaItem invalid = new MpvMediaItem(" ");
                    _ = invalid;
                },
                "媒體來源為空白應被拒絕");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證 <see cref="MpvCapabilities"/> POCO 的查詢方法與防呆行為。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyMpvCapabilities()
        {
            MpvCapabilities capabilities = new MpvCapabilities(
                new Version(2, 5),
                "mpv 0.41.0",
                "--enable-libmpv --enable-vulkan",
                new[] { "file", "http", "https", "ytdl" },
                Array.Empty<MpvDecoderInfo>(),
                new[] { "mp4", "matroska", "mpegts" });

            AssertEx.Equal(new Version(2, 5), capabilities.ClientApiVersion, "client API 版本");
            AssertEx.Equal("mpv 0.41.0", capabilities.MpvVersion, "mpv 版本字串");
            AssertEx.Equal(4, capabilities.Protocols.Count, "通訊協定數量");
            AssertEx.Equal(3, capabilities.Demuxers.Count, "demuxer 數量");
            AssertEx.True(capabilities.SupportsProtocol("https"), "應支援 https 協定");
            AssertEx.True(capabilities.SupportsProtocol("HTTPS"), "協定查詢應忽略大小寫");
            AssertEx.True(!capabilities.SupportsProtocol("rtmp"), "未列入的協定應回報不支援");
            AssertEx.True(!capabilities.SupportsProtocol(string.Empty), "空白協定應回報不支援");
            AssertEx.True(capabilities.ContainsDemuxer("mp4"), "應包含 mp4 demuxer");
            AssertEx.True(!capabilities.ContainsDemuxer("flv"), "未列入的 demuxer 應回報不存在");
            AssertEx.True(!capabilities.ContainsDecoder("h264"), "空解碼器清單應回報不存在");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證常用 yt-dlp 格式預設值會轉換成固定 selector。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyYtdlpFormatPresets()
        {
            AssertEx.Equal("ytdl", MpvYtdlpFormatSelector.FromPreset(MpvYtdlpFormatPreset.Default), "Default selector");
            AssertEx.Equal("bestvideo*+bestaudio/best", MpvYtdlpFormatSelector.FromPreset(MpvYtdlpFormatPreset.Best), "Best selector");
            AssertEx.Equal("bestaudio/best", MpvYtdlpFormatSelector.FromPreset(MpvYtdlpFormatPreset.AudioOnly), "AudioOnly selector");
            AssertEx.Equal("bestvideo*[height<=720]+bestaudio/best[height<=720]", MpvYtdlpFormatSelector.FromPreset(MpvYtdlpFormatPreset.UpTo720p), "720p selector");
            AssertEx.Equal("bestvideo*[height<=1080]+bestaudio/best[height<=1080]", MpvYtdlpFormatSelector.MaxHeight(1080), "MaxHeight selector");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證 yt-dlp 格式 selector helper 的輸入檢查。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyYtdlpFormatValidation()
        {
            AssertEx.Throws<ArgumentOutOfRangeException>(
                delegate
                {
                    MpvYtdlpFormatSelector.MaxHeight(0);
                },
                "MaxHeight 應拒絕零高度。");

            AssertEx.Throws<ArgumentOutOfRangeException>(
                delegate
                {
                    MpvYtdlpFormatSelector.FromPreset((MpvYtdlpFormatPreset)999);
                },
                "FromPreset 應拒絕未知列舉值。");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證 mpv encoding mode 高階選項會轉換成固定 mpv 選項。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyEncodingOptions()
        {
            MpvEncodingOptions encodingOptions = new MpvEncodingOptions("output file.mp4")
            {
                ContainerFormat = "mp4",
                ContainerFormatOptions = "movflags=faststart",
                VideoCodec = "libx264",
                VideoCodecOptions = "crf=23",
                AudioCodec = "aac",
                AudioCodecOptions = "b=128000",
                CopyRawTimestamps = true,
                CopyMetadata = false,
                Metadata = "title=輸出標題",
                RemovedMetadata = "comment"
            };
            encodingOptions.AdditionalOptions["omaxfps"] = "30";

            IReadOnlyDictionary<string, string> options = encodingOptions.ToOptionDictionary();
            AssertEx.Equal("output file.mp4", options["o"], "輸出檔案選項");
            AssertEx.Equal("mp4", options["of"], "輸出容器選項");
            AssertEx.Equal("movflags=faststart", options["ofopts"], "輸出容器參數");
            AssertEx.Equal("libx264", options["ovc"], "視訊編碼器選項");
            AssertEx.Equal("crf=23", options["ovcopts"], "視訊編碼器參數");
            AssertEx.Equal("aac", options["oac"], "音訊編碼器選項");
            AssertEx.Equal("b=128000", options["oacopts"], "音訊編碼器參數");
            AssertEx.Equal("yes", options["orawts"], "保留時間戳記選項");
            AssertEx.Equal("no", options["ocopy-metadata"], "複製中繼資料選項");
            AssertEx.Equal("title=輸出標題", options["oset-metadata"], "設定中繼資料選項");
            AssertEx.Equal("comment", options["oremove-metadata"], "移除中繼資料選項");
            AssertEx.Equal("30", options["omaxfps"], "額外 encoding 選項");

            MpvPlayerOptions playerOptions = new MpvPlayerOptions();
            playerOptions.ConfigureEncoding(encodingOptions);
            AssertEx.Equal("output file.mp4", playerOptions.InitialOptions["o"], "播放器輸出檔案選項");
            AssertEx.Equal("libx264", playerOptions.InitialOptions["ovc"], "播放器視訊編碼器選項");

            MpvEncodingOptions fluentEncoding = MpvEncodingOptions.ToFile("fluent.mp4")
                .AsMp4()
                .WithContainerOptions("movflags=faststart")
                .WithVideoCodec("libx264", "crf=20")
                .WithAudioCodec("aac", "b=96000")
                .CopyInputTimestamps()
                .CopyInputMetadata(false)
                .WithMetadata("title=鏈式輸出")
                .RemoveMetadata("comment")
                .WithOption("omaxfps", "24");
            IReadOnlyDictionary<string, string> fluentOptions = fluentEncoding.ToOptionDictionary();
            AssertEx.Equal("fluent.mp4", fluentOptions["o"], "鏈式輸出檔案選項");
            AssertEx.Equal("mp4", fluentOptions["of"], "鏈式輸出容器選項");
            AssertEx.Equal("crf=20", fluentOptions["ovcopts"], "鏈式視訊編碼器參數");
            AssertEx.Equal("b=96000", fluentOptions["oacopts"], "鏈式音訊編碼器參數");
            AssertEx.Equal("yes", fluentOptions["orawts"], "鏈式保留時間戳記選項");
            AssertEx.Equal("no", fluentOptions["ocopy-metadata"], "鏈式複製中繼資料選項");
            AssertEx.Equal("title=鏈式輸出", fluentOptions["oset-metadata"], "鏈式設定中繼資料選項");
            AssertEx.Equal("comment", fluentOptions["oremove-metadata"], "鏈式移除中繼資料選項");
            AssertEx.Equal("24", fluentOptions["omaxfps"], "鏈式額外 encoding 選項");

            AssertEx.Throws<InvalidOperationException>(
                delegate
                {
                    new MpvEncodingOptions(" ").ToOptionDictionary();
                },
                "空白輸出路徑應被拒絕。");

            AssertEx.Throws<InvalidOperationException>(
                delegate
                {
                    MpvEncodingOptions invalidOptions = new MpvEncodingOptions("output.mp4");
                    invalidOptions.AdditionalOptions[string.Empty] = "value";
                    invalidOptions.ToOptionDictionary();
                },
                "空白額外選項名稱應被拒絕。");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證播放器選項 fluent helper 會維持原選項物件並設定預期值。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyPlayerOptionFluentHelpers()
        {
            MpvEncodingOptions encodingOptions = MpvEncodingOptions.ToFile("encoded.mp4").AsMp4();
            MpvPlayerOptions options = new MpvPlayerOptions();
            MpvPlayerOptions returnedOptions = options
                .UseMpvLibraryPath("runtime\\libmpv-2.dll")
                .UseToolDirectory("runtime")
                .UseRuntimeConfiguration("runtime")
                .UseYtdlpFormat(MpvYtdlpFormatPreset.UpTo720p)
                .UseYtdlpFormat("bestvideo*+bestaudio/best")
                .UseYtdlpMaximumHeight(480)
                .AddConfigFile("mpv.conf")
                .AddScriptFile("scripts\\demo.lua")
                .WithInitialOption("terminal", "no")
                .UseEncoding(encodingOptions);

            AssertEx.True(object.ReferenceEquals(options, returnedOptions), "fluent helper 應傳回原本的播放器選項。");
            AssertEx.Equal("runtime\\libmpv-2.dll", options.MpvLibraryPath, "鏈式 libmpv 路徑");
            AssertEx.Equal("runtime", options.ToolDirectory, "鏈式工具資料夾");
            AssertEx.Equal("runtime", options.ConfigDirectory, "鏈式設定資料夾");
            AssertEx.True(options.LoadUserConfig, "鏈式設定載入應啟用。");
            AssertEx.Equal("bestvideo*[height<=480]+bestaudio/best[height<=480]", options.YtdlpFormat, "鏈式 yt-dlp 最高高度格式");
            AssertEx.Equal(MpvYtdlpFormatPreset.Default, options.YtdlpFormatPreset, "鏈式自訂格式應重設預設值");
            AssertEx.Equal("mpv.conf", options.ConfigFiles[0], "鏈式設定檔");
            AssertEx.Equal("scripts\\demo.lua", options.ScriptFiles[0], "鏈式腳本檔");
            AssertEx.Equal("no", options.InitialOptions["terminal"], "鏈式初始選項");
            AssertEx.Equal("encoded.mp4", options.InitialOptions["o"], "鏈式 encoding 輸出選項");
            AssertEx.Equal("mp4", options.InitialOptions["of"], "鏈式 encoding 容器選項");

            AssertEx.Throws<ArgumentException>(
                delegate
                {
                    new MpvPlayerOptions().UseMpvLibraryPath(" ");
                },
                "空白 libmpv 路徑應被拒絕。");

            AssertEx.Throws<ArgumentException>(
                delegate
                {
                    new MpvPlayerOptions().WithInitialOption(string.Empty, "value");
                },
                "空白初始選項名稱應被拒絕。");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證外部工具命令列引數格式化會處理空白、空字串與引號。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyExternalToolArgumentFormatting()
        {
            string formatted = ExternalToolProcessRunner.FormatArguments(new[] { "--flag", "hello world", string.Empty, "a\"b" });
            AssertEx.Equal("--flag \"hello world\" \"\" \"a\\\"b\"", formatted, "格式化後的命令列引數");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證強制 GitHub digest 策略會拒絕缺漏或不相符的 SHA-256 值。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyNativeAssetDigestValidation()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "native asset digest");
                string sha256 = DownloadUtility.ComputeSha256Hex(tempFile);
                DownloadUtility.VerifyDownloadedAsset(
                    tempFile,
                    "sha256:" + sha256,
                    true,
                    MpvNativeAssetVerificationPolicy.RequireGitHubDigest,
                    null,
                    "asset.bin");

                AssertEx.Throws<InvalidOperationException>(
                    delegate
                    {
                        DownloadUtility.VerifyDownloadedAsset(
                            tempFile,
                            null,
                            true,
                            MpvNativeAssetVerificationPolicy.RequireGitHubDigest,
                            null,
                            "asset.bin");
                    },
                    "強制 digest 策略應拒絕缺漏的 GitHub digest。");

                AssertEx.Throws<InvalidOperationException>(
                    delegate
                    {
                        DownloadUtility.VerifyDownloadedAsset(
                            tempFile,
                            "sha256:" + new string('0', 64),
                            true,
                            MpvNativeAssetVerificationPolicy.RequireGitHubDigest,
                            null,
                            "asset.bin");
                    },
                    "強制 digest 策略應拒絕不相符的 GitHub digest。");
            }
            finally
            {
                File.Delete(tempFile);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證釘選 SHA-256 策略會要求呼叫端提供預期值並比對下載內容。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyNativeAssetPinnedSha256Validation()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "native asset pinned sha256");
                string sha256 = DownloadUtility.ComputeSha256Hex(tempFile);
                DownloadUtility.VerifyDownloadedAsset(
                    tempFile,
                    null,
                    false,
                    MpvNativeAssetVerificationPolicy.RequirePinnedSha256,
                    sha256,
                    "asset.bin");

                AssertEx.Throws<InvalidOperationException>(
                    delegate
                    {
                        DownloadUtility.VerifyDownloadedAsset(
                            tempFile,
                            null,
                            false,
                            MpvNativeAssetVerificationPolicy.RequirePinnedSha256,
                            null,
                            "asset.bin");
                    },
                    "釘選 SHA-256 策略應要求預期值。");
            }
            finally
            {
                File.Delete(tempFile);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證 GNU 風格 checksum 檔案解析支援 yt-dlp、Deno 與 FFmpeg 常見格式。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyNativeAssetChecksumParsing()
        {
            string expected = new string('a', 64);
            string other = new string('b', 64);
            string checksumText = "# comment" + Environment.NewLine +
                other + "  other.exe" + Environment.NewLine +
                expected + " *yt-dlp.exe" + Environment.NewLine;
            AssertEx.Equal(expected, DownloadUtility.FindSha256InChecksumText(checksumText, "yt-dlp.exe"), "yt-dlp checksum 解析");

            string denoChecksumText = expected + "  deno-x86_64-pc-windows-msvc.zip";
            AssertEx.Equal(expected, DownloadUtility.FindSha256InChecksumText(denoChecksumText, "deno-x86_64-pc-windows-msvc.zip"), "Deno checksum 解析");

            string ffmpegChecksumText = expected + "  " + FFmpegDownloader.WindowsX64AssetName;
            AssertEx.Equal(expected, DownloadUtility.FindSha256InChecksumText(ffmpegChecksumText, FFmpegDownloader.WindowsX64AssetName), "FFmpeg checksum 解析");

            string singleChecksumText = expected;
            AssertEx.Equal(expected, DownloadUtility.FindSha256InChecksumText(singleChecksumText, "asset.zip"), "單一 checksum 解析");

            AssertEx.Throws<InvalidOperationException>(
                delegate
                {
                    DownloadUtility.FindSha256InChecksumText(checksumText, "missing.exe");
                },
                "checksum 應拒絕不存在的資產名稱。");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證來源鎖定會接受預設 GitHub 來源並拒絕非預期來源。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyNativeAssetSourceLockValidation()
        {
            Uri expectedApiUri = new Uri("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            DownloadUtility.ValidateLockedGitHubSource(
                expectedApiUri,
                expectedApiUri,
                "https://github.com/yt-dlp/yt-dlp/releases/download/2026.03.17/yt-dlp.exe",
                "yt-dlp",
                "yt-dlp",
                true);

            AssertEx.Throws<InvalidOperationException>(
                delegate
                {
                    DownloadUtility.ValidateLockedGitHubSource(
                        new Uri("https://api.github.com/repos/example/fork/releases/latest"),
                        expectedApiUri,
                        "https://github.com/yt-dlp/yt-dlp/releases/download/2026.03.17/yt-dlp.exe",
                        "yt-dlp",
                        "yt-dlp",
                        true);
                },
                "來源鎖定應拒絕非預設 API。");

            AssertEx.Throws<InvalidOperationException>(
                delegate
                {
                    DownloadUtility.ValidateLockedGitHubSource(
                        expectedApiUri,
                        expectedApiUri,
                        "https://github.com/example/fork/releases/download/2026.03.17/yt-dlp.exe",
                        "yt-dlp",
                        "yt-dlp",
                        true);
                },
                "來源鎖定應拒絕非預期下載 URL。");

            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證 runtime 下載選項的驗證策略預設值保持相容。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyRuntimeVerificationOptionDefaults()
        {
            YtDlpDownloadOptions ytDlp = new YtDlpDownloadOptions();
            DenoDownloadOptions deno = new DenoDownloadOptions();
            FFmpegDownloadOptions ffmpeg = new FFmpegDownloadOptions();
            MpvWindowsBuildDownloadOptions libMpv = new MpvWindowsBuildDownloadOptions();

            AssertEx.Equal(MpvNativeAssetVerificationPolicy.BestEffort, ytDlp.VerificationPolicy, "yt-dlp 驗證策略預設值");
            AssertEx.Equal(MpvNativeAssetVerificationPolicy.BestEffort, deno.VerificationPolicy, "Deno 驗證策略預設值");
            AssertEx.Equal(MpvNativeAssetVerificationPolicy.BestEffort, ffmpeg.VerificationPolicy, "FFmpeg 驗證策略預設值");
            AssertEx.Equal(MpvNativeAssetVerificationPolicy.BestEffort, libMpv.VerificationPolicy, "libmpv 驗證策略預設值");
            AssertEx.True(ytDlp.VerifyDigest, "yt-dlp 應預設驗證可用 digest。");
            AssertEx.True(deno.VerifyDigest, "Deno 應預設驗證可用 digest。");
            AssertEx.True(ffmpeg.VerifyDigest, "FFmpeg 應預設驗證可用 digest。");
            AssertEx.True(libMpv.VerifyDigest, "libmpv 應預設驗證可用 digest。");
            AssertEx.False(ytDlp.LockReleaseSource, "yt-dlp 不應預設鎖定來源。");
            AssertEx.False(deno.LockReleaseSource, "Deno 不應預設鎖定來源。");
            AssertEx.False(ffmpeg.LockReleaseSource, "FFmpeg 不應預設鎖定來源。");
            AssertEx.False(libMpv.LockReleaseSource, "libmpv 不應預設鎖定來源。");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證 Windows runtime helper 預設包含 FFmpeg，且可由呼叫端關閉。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyWindowsRuntimeFFmpegOptionDefaults()
        {
            MpvWindowsRuntimeDownloadOptions options = new MpvWindowsRuntimeDownloadOptions();
            AssertEx.True(options.IncludeFFmpeg, "Windows runtime 預設應包含 FFmpeg。");
            options.IncludeFFmpeg = false;
            AssertEx.False(options.IncludeFFmpeg, "Windows runtime 應允許關閉 FFmpeg 下載。");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證播放器選項的預設值維持穩定。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyPlayerOptionDefaults()
        {
            MpvPlayerOptions options = new MpvPlayerOptions();
            AssertEx.True(options.EnableDefaultInputBindings, "預設應啟用輸入繫結。");
            AssertEx.True(options.EnableKeyboardInput, "預設應啟用鍵盤輸入。");
            AssertEx.True(options.EnableOsc, "預設應啟用 OSC。");
            AssertEx.True(options.EnableYtdlp, "預設應啟用 yt-dlp。");
            AssertEx.Equal("yt-dlp;youtube-dl", options.YtdlpPath, "預設 yt-dlp 搜尋路徑");
            AssertEx.Equal(MpvYtdlpFormatPreset.Default, options.YtdlpFormatPreset, "預設 yt-dlp 格式");
            AssertEx.Equal("warn", options.LogLevel, "預設記錄等級");
            AssertEx.Equal(0, options.InitialOptions.Count, "預設初始選項數量");
            AssertEx.Equal(0, options.ConfigFiles.Count, "預設設定檔數量");
            AssertEx.Equal(0, options.ScriptFiles.Count, "預設腳本數量");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證目前 catalog 只宣告 Windows x64 來源。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyRuntimeCatalogs()
        {
            IReadOnlyList<MpvNativeRuntimeSource> windowsSources = MpvNativeRuntimeCatalog.GetSources(MpvNativeRuntimePlatform.Windows);
            IReadOnlyList<MpvNativeRuntimeSource> unknownSources = MpvNativeRuntimeCatalog.GetSources(MpvNativeRuntimePlatform.Unknown);
            AssertEx.Equal(2, windowsSources.Count, "Windows libmpv 來源數量");
            AssertEx.Equal(0, unknownSources.Count, "未知平台 libmpv 來源數量");
            AssertEx.Equal(MpvNativeRuntimeSupportStatus.Supported, MpvNativeRuntimeCatalog.GetProjectSupportStatus(MpvNativeRuntimePlatform.Windows), "Windows 支援狀態");
            AssertEx.Equal(MpvNativeRuntimeSupportStatus.NotCataloged, MpvNativeRuntimeCatalog.GetProjectSupportStatus(MpvNativeRuntimePlatform.Unknown), "未知平台支援狀態");

            IReadOnlyList<ExternalToolRuntimeSource> ytDlpSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.YtDlp, MpvNativeRuntimePlatform.Windows);
            IReadOnlyList<ExternalToolRuntimeSource> denoSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.Deno, MpvNativeRuntimePlatform.Windows);
            IReadOnlyList<ExternalToolRuntimeSource> ffmpegSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.FFmpeg, MpvNativeRuntimePlatform.Windows);
            IReadOnlyList<ExternalToolRuntimeSource> unknownToolSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.YtDlp, MpvNativeRuntimePlatform.Unknown);
            IReadOnlyList<ExternalToolRuntimeSource> unknownFFmpegSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.FFmpeg, MpvNativeRuntimePlatform.Unknown);
            AssertEx.Equal(1, ytDlpSources.Count, "Windows yt-dlp 來源數量");
            AssertEx.Equal(1, denoSources.Count, "Windows Deno 來源數量");
            AssertEx.Equal(1, ffmpegSources.Count, "Windows FFmpeg 來源數量");
            AssertEx.Equal(0, unknownToolSources.Count, "未知平台外部工具來源數量");
            AssertEx.Equal(0, unknownFFmpegSources.Count, "未知平台 FFmpeg 來源數量");
            AssertEx.True(ytDlpSources[0].SupportsSelfUpdate, "yt-dlp 應提供自我更新命令。");
            AssertEx.True(denoSources[0].SupportsSelfUpdate, "Deno 應提供自我更新命令。");
            AssertEx.False(ffmpegSources[0].SupportsSelfUpdate, "FFmpeg 不應宣告內建自我更新命令。");
            AssertEx.Equal(FFmpegDownloader.WindowsX64AssetName, ffmpegSources[0].AssetName, "FFmpeg catalog 應指向 Windows x64 GPL 資產。");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 驗證未知平台安裝流程只回傳不支援結果，不建立下載資料夾。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static async Task VerifyUnknownPlatformInstallAsync()
        {
            string runtimeDirectory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.Tests", Guid.NewGuid().ToString("N"));
            MpvRuntimeInstallOptions options = new MpvRuntimeInstallOptions
            {
                Platform = MpvNativeRuntimePlatform.Unknown
            };

            MpvRuntimeInstallResult result = await MpvRuntimeInstaller.InstallOrUpdateAsync(runtimeDirectory, options).ConfigureAwait(false);
            AssertEx.False(result.IsSupported, "未知平台不應標示為已支援。");
            AssertEx.Equal(MpvNativeRuntimeSupportStatus.NotCataloged, result.Status, "未知平台安裝狀態");
            AssertEx.Equal(0, result.NativeSources.Count, "未知平台來源數量");
            AssertEx.False(Directory.Exists(runtimeDirectory), "未知平台不應建立執行階段資料夾。");
        }

        /// <summary>
        /// 驗證 Windows 執行階段資料夾會產生正確播放器選項。
        /// </summary>
        /// <returns>代表測試流程的工作。</returns>
        private static Task VerifyWindowsRuntimePlayerOptions()
        {
            string runtimeDirectory = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.Runtime");
            MpvPlayerOptions options = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory, true);
            AssertEx.Equal(Path.Combine(runtimeDirectory, "libmpv-2.dll"), options.MpvLibraryPath, "libmpv 路徑");
            AssertEx.Equal(runtimeDirectory, options.ToolDirectory, "工具資料夾");
            AssertEx.Equal(runtimeDirectory, options.ConfigDirectory, "設定資料夾");
            AssertEx.True(options.LoadUserConfig, "應載入使用者設定。");
            AssertEx.True(options.YtdlpPath.StartsWith(Path.Combine(runtimeDirectory, "yt-dlp.exe"), StringComparison.OrdinalIgnoreCase), "yt-dlp 路徑應優先指向執行階段資料夾。");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 提供簡易測試執行器。
    /// </summary>
    internal sealed class TestRunner
    {
        /// <summary>
        /// 保存要執行的測試案例。
        /// </summary>
        private readonly List<TestCase> _tests = new List<TestCase>();

        /// <summary>
        /// 取得失敗測試數量。
        /// </summary>
        /// <value>失敗測試數量。</value>
        public int FailedCount { get; private set; }

        /// <summary>
        /// 加入測試案例。
        /// </summary>
        /// <param name="name">測試名稱。</param>
        /// <param name="body">測試主體。</param>
        public void Add(string name, Func<Task> body)
        {
            _tests.Add(new TestCase(name, body));
        }

        /// <summary>
        /// 依序執行所有測試案例。
        /// </summary>
        /// <returns>代表測試執行流程的工作。</returns>
        public async Task RunAsync()
        {
            foreach (TestCase test in _tests)
            {
                try
                {
                    await test.Body().ConfigureAwait(false);
                    Console.WriteLine("[PASS] " + test.Name);
                }
                catch (Exception ex)
                {
                    FailedCount++;
                    Console.WriteLine("[FAIL] " + test.Name + " - " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            Console.WriteLine("測試完成：通過 " + (_tests.Count - FailedCount).ToString(System.Globalization.CultureInfo.InvariantCulture) + "，失敗 " + FailedCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "。");
        }
    }

    /// <summary>
    /// 表示一個測試案例。
    /// </summary>
    internal sealed class TestCase
    {
        /// <summary>
        /// 初始化 <see cref="TestCase"/> 類別的新執行個體。
        /// </summary>
        /// <param name="name">測試名稱。</param>
        /// <param name="body">測試主體。</param>
        public TestCase(string name, Func<Task> body)
        {
            Name = name;
            Body = body;
        }

        /// <summary>
        /// 取得測試名稱。
        /// </summary>
        /// <value>測試名稱。</value>
        public string Name { get; private set; }

        /// <summary>
        /// 取得測試主體。
        /// </summary>
        /// <value>測試主體。</value>
        public Func<Task> Body { get; private set; }
    }

    /// <summary>
    /// 提供測試斷言方法。
    /// </summary>
    internal static class AssertEx
    {
        /// <summary>
        /// 驗證兩個值相等。
        /// </summary>
        /// <typeparam name="T">要比較的值型別。</typeparam>
        /// <param name="expected">預期值。</param>
        /// <param name="actual">實際值。</param>
        /// <param name="message">失敗時顯示的訊息。</param>
        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + "。預期：" + expected + "，實際：" + actual);
            }
        }

        /// <summary>
        /// 驗證條件為真。
        /// </summary>
        /// <param name="condition">要驗證的條件。</param>
        /// <param name="message">失敗時顯示的訊息。</param>
        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// 驗證條件為假。
        /// </summary>
        /// <param name="condition">要驗證的條件。</param>
        /// <param name="message">失敗時顯示的訊息。</param>
        public static void False(bool condition, string message)
        {
            if (condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// 驗證指定動作會擲回指定例外狀況。
        /// </summary>
        /// <typeparam name="TException">預期的例外狀況型別。</typeparam>
        /// <param name="action">要執行的動作。</param>
        /// <param name="message">失敗時顯示的訊息。</param>
        public static void Throws<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }
}
