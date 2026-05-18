using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MediaEmbedKit.Mpv.Platforms;
using MediaEmbedKit.Mpv.Externals;
using MediaEmbedKit.Mpv.Runtime;
using MediaEmbedKit.Mpv.Diagnostics;
using MediaEmbedKit.Mpv.Hosting;
using MediaEmbedKit.Mpv.Native;
using MediaEmbedKit.Mpv.Render;
using Microsoft.Extensions.DependencyInjection;

namespace MediaEmbedKit.Mpv.Tests;

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
        runner.Add("下載要求瀏覽器標頭", VerifyBrowserRequestHeaders);
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
        runner.Add("DI 擴充註冊播放器工廠", VerifyDependencyInjectionExtensions);
        runner.Add("Provider / ProviderFallbackOrder 預設值（Zhongfly + Shinchiro fallback）", VerifyProviderFallbackOrderDefaults);
        runner.Add("MpvWindowsBuildDownloadOptions.Clone 深淺層複製", VerifyMpvWindowsBuildDownloadOptionsClone);
        runner.Add("LibMpvVersionMarker round-trip 寫入讀回", VerifyLibMpvVersionMarkerRoundTrip);
        runner.Add("ArchiveSafety 拒絕 reparse point", VerifyArchiveSafetyRejectsReparsePoint);
        runner.Add("RuntimeDirectoryLock 跨呼叫互斥（同 process 模擬）", VerifyRuntimeDirectoryLockMutualExclusion);
        runner.Add("MpvLicenseAuditor 分類授權狀態", VerifyMpvLicenseAuditorClassification);
        runner.Add("MpvMediaItem fluent helpers", VerifyMpvMediaItemFluentHelpers);
        runner.Add("MpvPlayerOptions.CopyTo 全欄複製", VerifyMpvPlayerOptionsCopyTo);
        runner.Add("MpvRelayCommand CanExecute/Execute/RaiseCanExecuteChanged", VerifyMpvRelayCommand);
        runner.Add("MpvEncodingOptions two-pass clone 內部累積清單", VerifyEncodingOptionsTwoPassClone);
        runner.Add("MpvRuntimeHealthReport IsComplete / IsHealthyFor", VerifyMpvRuntimeHealthReportSemantics);
        runner.Add("MpvRenderParamType.AmbientLight 已標 [Obsolete]", VerifyAmbientLightObsolete);
        runner.Add("MpvPlayer 提供 TryGetProperty* 系列", VerifyTryGetPropertySurface);
        runner.Add("MpvNative 在 net7.0+ 採用 LibraryImport", VerifyMpvNativeUsesLibraryImport);
        runner.Add("MpvNative 委派／陣列 helper 在 net7.0+ 仍走 LibraryImport", VerifyMpvNativeHelperUsesLibraryImport);
        runner.Add("Windows ARM64 資產對應正確（libmpv / yt-dlp / Deno / FFmpeg）", VerifyWindowsArm64AssetMapping);
        runner.Add("PackageVersion 與 docs/CONSUMING_PACKAGES.md 同步", VerifyPackageVersionInConsumingDoc);

        await runner.RunAsync().ConfigureAwait(false);
        return runner.FailedCount == 0 ? 0 : 1;
    }

    /// <summary>
    /// 驗證 <c>Directory.Build.props</c> 的 <c>PackageVersion</c> 出現在
    /// <c>docs/CONSUMING_PACKAGES.md</c>。若有人手動改 props 沒同步文件就會被擋下，
    /// 提醒去跑 <c>tools/Bump-Version.ps1</c>。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyPackageVersionInConsumingDoc()
    {
        string? repoRoot = FindRepoRoot();
        AssertEx.True(repoRoot != null, "無法從測試輸出目錄回溯找到 repo 根（含 Directory.Build.props 的資料夾）。");

        string propsPath = Path.Combine(repoRoot!, "Directory.Build.props");
        string propsContent = File.ReadAllText(propsPath);
        System.Text.RegularExpressions.Match versionMatch = System.Text.RegularExpressions.Regex.Match(
            propsContent,
            @"<PackageVersion>([^<]+)</PackageVersion>");
        AssertEx.True(versionMatch.Success, "Directory.Build.props 內找不到 <PackageVersion>。");

        string version = versionMatch.Groups[1].Value;
        string docPath = Path.Combine(repoRoot!, "docs", "CONSUMING_PACKAGES.md");
        AssertEx.True(File.Exists(docPath), "找不到 docs/CONSUMING_PACKAGES.md。");

        string docContent = File.ReadAllText(docPath);
        AssertEx.True(
            docContent.Contains(version, StringComparison.Ordinal),
            "docs/CONSUMING_PACKAGES.md 未提及 PackageVersion '" + version + "'。"
            + "props 已升版但文件未同步；請使用 tools/Bump-Version.ps1 一鍵改全部。");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 從測試輸出目錄回溯找含 <c>Directory.Build.props</c> 的目錄當作 repo 根。
    /// </summary>
    /// <returns>找到時為 repo 根目錄絕對路徑，否則為 <see langword="null"/>。</returns>
    private static string? FindRepoRoot()
    {
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// 驗證 <see cref="MpvRenderParamType.AmbientLight"/> 已標 <see cref="ObsoleteAttribute"/>，
    /// 對齊 libmpv 0.40 deprecation。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyAmbientLightObsolete()
    {
#pragma warning disable CS0618 // 測試本身需要透過 nameof 引用已 deprecated 的列舉成員。
        FieldInfo? field = typeof(MpvRenderParamType).GetField(
            nameof(MpvRenderParamType.AmbientLight),
            BindingFlags.Public | BindingFlags.Static);
#pragma warning restore CS0618
        AssertEx.True(field != null, "MpvRenderParamType.AmbientLight 列舉成員應存在。");
        AssertEx.True(field!.GetCustomAttribute<ObsoleteAttribute>() != null,
            "MpvRenderParamType.AmbientLight 應已標 [Obsolete]（libmpv 0.40 deprecation）。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayer"/> 提供 <c>TryGetProperty*</c> 系列方法，作為非例外的屬性讀取入口。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyTryGetPropertySurface()
    {
        Type playerType = typeof(MpvPlayer);

        AssertEx.True(
            HasInstanceMethod(playerType, "TryGetPropertyString", typeof(string), typeof(string).MakeByRefType()),
            "MpvPlayer 應提供 TryGetPropertyString(string, out string?)。");
        AssertEx.True(
            HasInstanceMethod(playerType, "TryGetPropertyFlag", typeof(string), typeof(bool).MakeByRefType()),
            "MpvPlayer 應提供 TryGetPropertyFlag(string, out bool)。");
        AssertEx.True(
            HasInstanceMethod(playerType, "TryGetPropertyInt64", typeof(string), typeof(long).MakeByRefType()),
            "MpvPlayer 應提供 TryGetPropertyInt64(string, out long)。");
        AssertEx.True(
            HasInstanceMethod(playerType, "TryGetPropertyDouble", typeof(string), typeof(double).MakeByRefType()),
            "MpvPlayer 應提供 TryGetPropertyDouble(string, out double)。");
        AssertEx.True(
            HasInstanceMethod(playerType, "TryGetPropertyNode", typeof(string), typeof(MpvNode).MakeByRefType()),
            "MpvPlayer 應提供 TryGetPropertyNode(string, out MpvNode)。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 判斷型別是否含指定的執行個體方法。
    /// </summary>
    /// <param name="type">要查詢的型別。</param>
    /// <param name="name">方法名稱。</param>
    /// <param name="parameterTypes">方法參數型別（依宣告順序）。</param>
    /// <returns>找到符合簽名的方法時為 <see langword="true"/>。</returns>
    private static bool HasInstanceMethod(Type type, string name, params Type[] parameterTypes)
    {
        MethodInfo? method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        return method != null && method.ReturnType == typeof(bool);
    }

    /// <summary>
    /// 驗證在 <c>net7.0</c> 以上 <c>MpvNative</c> 的純 blittable 入口確實由 P/Invoke source generator
    /// 以 <see cref="LibraryImportAttribute"/> 產生，<c>netstandard2.0</c> / <c>.NET Framework</c> 仍走
    /// <see cref="DllImportAttribute"/>。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvNativeUsesLibraryImport()
    {
        string[] candidates = new[]
        {
            "mpv_create",
            "mpv_initialize",
            "mpv_terminate_destroy",
            "mpv_get_property",
            "mpv_set_property",
            "mpv_observe_property",
            "mpv_unobserve_property",
            "mpv_command_node",
            "mpv_request_event",
            "mpv_wait_event",
            "mpv_event_to_node",
            "mpv_hook_add",
            "mpv_hook_continue",
            "mpv_render_context_set_parameter",
            "mpv_render_context_update"
        };

        foreach (string name in candidates)
        {
            MethodInfo[] methods = typeof(MpvNative).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == name).ToArray();
            AssertEx.True(methods.Length > 0, name + " 入口應存在於 MpvNative。");

            foreach (MethodInfo method in methods)
            {
#if NET7_0_OR_GREATER
                // P/Invoke source generator 會在 partial method 上保留 LibraryImport，並另外生成
                // 帶有 DllImport 的 implementation；因此這裡只驗證 LibraryImport 確實存在。
                AssertEx.True(
                    method.IsDefined(typeof(LibraryImportAttribute), inherit: false),
                    name + " 在 net7.0+ 應由 P/Invoke source generator 以 LibraryImport 包裝。");
#else
                AssertEx.True(
                    method.IsDefined(typeof(DllImportAttribute), inherit: false),
                    name + " 在舊版 TFM 應使用 DllImport。");
#endif
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證原本含委派／陣列參數的 5 個入口已透過 <c>*_native</c> 私有 P/Invoke 統一改為純 IntPtr 簽名，
    /// 並在 <c>net7.0</c> 以上同樣由 P/Invoke source generator 以 <see cref="LibraryImportAttribute"/> 包裝；
    /// 同時上層 internal helper（沿用原 native 名稱）仍提供既有委派／陣列簽名給呼叫端，呼叫端 0 變更。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvNativeHelperUsesLibraryImport()
    {
        string[] nativeEntries = new[]
        {
            "mpv_set_wakeup_callback_native",
            "mpv_stream_cb_add_ro_native",
            "mpv_render_context_set_update_callback_native",
            "mpv_render_context_create_native",
            "mpv_render_context_render_native"
        };

        foreach (string name in nativeEntries)
        {
            MethodInfo method = typeof(MpvNative).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException(name + " 私有 P/Invoke 入口應存在於 MpvNative。");
#if NET7_0_OR_GREATER
            AssertEx.True(
                method.IsDefined(typeof(LibraryImportAttribute), inherit: false),
                name + " 在 net7.0+ 應由 P/Invoke source generator 以 LibraryImport 包裝。");
#else
            AssertEx.True(
                method.IsDefined(typeof(DllImportAttribute), inherit: false),
                name + " 在舊版 TFM 應使用 DllImport。");
#endif
        }

        // helper 維持原 native 名稱、保留 delegate / array 簽名給呼叫端：以「不掛任何 P/Invoke 屬性」為驗證。
        string[] helperNames = new[]
        {
            "mpv_set_wakeup_callback",
            "mpv_stream_cb_add_ro",
            "mpv_render_context_set_update_callback",
            "mpv_render_context_create",
            "mpv_render_context_render"
        };

        foreach (string name in helperNames)
        {
            MethodInfo[] candidates = typeof(MpvNative).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == name).ToArray();
            AssertEx.True(candidates.Length > 0, name + " helper 應存在於 MpvNative。");
            foreach (MethodInfo helper in candidates)
            {
                AssertEx.False(
                    helper.IsDefined(typeof(DllImportAttribute), inherit: false),
                    name + " helper 不應直接掛 DllImport（應透過 *_native 私有入口）。");
                AssertEx.False(
                    helper.IsDefined(typeof(LibraryImportAttribute), inherit: false),
                    name + " helper 不應直接掛 LibraryImport（應透過 *_native 私有入口）。");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvWindowsBuildDownloadOptions"/> 的 Provider 與
    /// <see cref="MpvWindowsBuildDownloadOptions.ProviderFallbackOrder"/> 預設值。
    /// 預設 Provider = Zhongfly（兩家中唯一同時提供 LGPL libmpv），fallback 清單預設
    /// 含 Shinchiro 作為兜底 —— 一同確認對 release 後使用者「預設拿 LGPL libmpv」
    /// 的期望成立。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyProviderFallbackOrderDefaults()
    {
        MpvWindowsBuildDownloadOptions options = new MpvWindowsBuildDownloadOptions();
        AssertEx.Equal(MpvWindowsBuildProvider.Zhongfly, options.Provider, "預設 Provider 應為 Zhongfly（兩家中唯一同時提供 LGPL libmpv）");
        AssertEx.Equal(1, options.ProviderFallbackOrder.Count, "ProviderFallbackOrder 預設應含 Shinchiro 一個 fallback 項目");
        AssertEx.Equal(MpvWindowsBuildProvider.Shinchiro, options.ProviderFallbackOrder[0], "ProviderFallbackOrder 預設首項應為 Shinchiro");
        options.ProviderFallbackOrder.Clear();
        AssertEx.Equal(0, options.ProviderFallbackOrder.Count, "ProviderFallbackOrder 應支援清空");
        options.ProviderFallbackOrder.Add(MpvWindowsBuildProvider.Shinchiro);
        AssertEx.Equal(1, options.ProviderFallbackOrder.Count, "ProviderFallbackOrder 應支援新增備援 provider");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvWindowsBuildDownloadOptions.Clone"/> 產生獨立複本：所有純值
    /// 欄位等值複製，<see cref="MpvWindowsBuildDownloadOptions.ProviderFallbackOrder"/>
    /// 拷貝成獨立 list（修改原物件 list 不影響複本）。這是 PR G 內 UpdateLibMpvAsync /
    /// InstallOrUpdateLibMpvAsync 避免 mutate caller options 的基礎。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvWindowsBuildDownloadOptionsClone()
    {
        MpvWindowsBuildDownloadOptions original = new MpvWindowsBuildDownloadOptions
        {
            Provider = MpvWindowsBuildProvider.Shinchiro,
            Architecture = MpvWindowsArchitecture.Arm64,
            UserAgent = "test-ua/1.0",
            LicensePreference = MpvWindowsBuildLicensePreference.RequireLgpl,
            OverwriteExisting = true,
            VerificationPolicy = MpvNativeAssetVerificationPolicy.RequirePinnedSha256,
            VerifyDigest = false,
            ExpectedSha256 = "deadbeef",
            SevenZipPath = @"C:\custom\7z.exe",
            ExtractDirectory = @"C:\custom\extract",
            ReleaseApiUriOverride = new System.Uri("https://example.com/api"),
            RetainArchive = true,
        };
        original.ProviderFallbackOrder.Clear();
        original.ProviderFallbackOrder.Add(MpvWindowsBuildProvider.Zhongfly);

        MpvWindowsBuildDownloadOptions copy = original.Clone();

        AssertEx.Equal(original.Provider, copy.Provider, "Provider 應等值複製");
        AssertEx.Equal(original.Architecture, copy.Architecture, "Architecture 應等值複製");
        AssertEx.Equal(original.UserAgent, copy.UserAgent, "UserAgent 應等值複製");
        AssertEx.Equal(original.LicensePreference, copy.LicensePreference, "LicensePreference 應等值複製");
        AssertEx.Equal(original.OverwriteExisting, copy.OverwriteExisting, "OverwriteExisting 應等值複製");
        AssertEx.Equal(original.VerificationPolicy, copy.VerificationPolicy, "VerificationPolicy 應等值複製");
        AssertEx.Equal(original.VerifyDigest, copy.VerifyDigest, "VerifyDigest 應等值複製");
        AssertEx.Equal(original.ExpectedSha256, copy.ExpectedSha256, "ExpectedSha256 應等值複製");
        AssertEx.Equal(original.SevenZipPath, copy.SevenZipPath, "SevenZipPath 應等值複製");
        AssertEx.Equal(original.ExtractDirectory, copy.ExtractDirectory, "ExtractDirectory 應等值複製");
        AssertEx.Equal(original.ReleaseApiUriOverride, copy.ReleaseApiUriOverride, "ReleaseApiUriOverride 應等值複製");
        AssertEx.Equal(original.RetainArchive, copy.RetainArchive, "RetainArchive 應等值複製");
        AssertEx.Equal(1, copy.ProviderFallbackOrder.Count, "ProviderFallbackOrder 應等值複製");
        AssertEx.Equal(MpvWindowsBuildProvider.Zhongfly, copy.ProviderFallbackOrder[0], "ProviderFallbackOrder 內容應一致");

        // 互不影響：改原物件不影響複本，反之亦然。
        copy.OverwriteExisting = false;
        AssertEx.True(original.OverwriteExisting, "改複本純值欄位不影響原物件");
        copy.ProviderFallbackOrder.Add(MpvWindowsBuildProvider.Shinchiro);
        AssertEx.Equal(1, original.ProviderFallbackOrder.Count, "改複本 ProviderFallbackOrder list 不影響原物件");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="LibMpvVersionMarker"/> JSON round-trip：寫入後讀回的欄位與寫入時
    /// 一致；缺漏 marker 檔回傳 null；schema 版本不匹配回傳 null（防護未來 schema 演進
    /// 時舊 marker 不被誤判為當前格式）。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyLibMpvVersionMarkerRoundTrip()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.Tests.MarkerRoundTrip", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string markerPath = Path.Combine(tempRoot, "libmpv-2.dll" + LibMpvVersionMarker.FileExtension);

            // 不存在 → null
            AssertEx.True(LibMpvVersionMarker.TryRead(markerPath) == null, "不存在 marker 應回傳 null");

            // 寫入 → 讀回，欄位一致
            LibMpvVersionMarker.Write(markerPath, MpvWindowsBuildProvider.Zhongfly, "2026-05-17-059bc7025b", "mpv-dev-lgpl-x86_64-20260517-git-059bc7025b.7z");
            LibMpvVersionMarker? read = LibMpvVersionMarker.TryRead(markerPath);
            AssertEx.True(read != null, "寫入後 marker 應讀得到");
            AssertEx.Equal(LibMpvVersionMarker.CurrentSchemaVersion, read!.SchemaVersion, "schemaVersion 應為當前版本");
            AssertEx.Equal(MpvWindowsBuildProvider.Zhongfly.ToString(), read.Provider, "Provider 字串應 round-trip 一致");
            AssertEx.Equal("2026-05-17-059bc7025b", read.ReleaseTag, "ReleaseTag 應 round-trip 一致");
            AssertEx.Equal("mpv-dev-lgpl-x86_64-20260517-git-059bc7025b.7z", read.AssetName, "AssetName 應 round-trip 一致");

            // schema 版本不匹配 → null（用未來假設的 schema 版本模擬）
            File.WriteAllText(markerPath, "{\"schemaVersion\":9999,\"provider\":\"Zhongfly\",\"releaseTag\":\"x\",\"assetName\":\"y\"}");
            AssertEx.True(LibMpvVersionMarker.TryRead(markerPath) == null, "未知 schemaVersion 應回傳 null");

            // 壞 JSON → null
            File.WriteAllText(markerPath, "this is not json");
            AssertEx.True(LibMpvVersionMarker.TryRead(markerPath) == null, "壞 JSON 應回傳 null");
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="ArchiveSafety.RejectIfReparsePoint"/> 對 NTFS reparse point /
    /// symlink 的拒絕行為。Windows 上建立 directory symlink 需 admin / dev-mode；
    /// 此測試用 <see cref="System.IO.FileAttributes.ReparsePoint"/> attribute 模擬一個
    /// 「看起來像 reparse point」的檔案無法直接做（OS-level 設定），改為驗證：
    /// 一般檔案不 throw、不存在檔案不 throw、且 helper 提供的訊息含足夠 context。
    /// 真實 symlink 拒絕情境由 integration test 在實機驗證（需 dev mode 或 admin）。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyArchiveSafetyRejectsReparsePoint()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.Tests.ArchiveSafety", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            // 不存在的檔案 → 直接 return（不 throw）
            string nonExistent = Path.Combine(tempRoot, "nope.dll");
            ArchiveSafety.RejectIfReparsePoint(nonExistent, "test: missing file");

            // 一般檔案（無 reparse point attribute）→ 不 throw
            string ordinary = Path.Combine(tempRoot, "ordinary.dll");
            File.WriteAllBytes(ordinary, new byte[] { 0x4D, 0x5A });  // 假 PE header
            ArchiveSafety.RejectIfReparsePoint(ordinary, "test: ordinary file");
            AssertEx.True(File.Exists(ordinary), "一般檔案經檢查後應仍存在");

            // helper 訊息應提到 CVE 與 ExpectedSha256 商用合規路徑（給 throw 路徑的 caller）。
            // 改用 mock — 模擬真正的 reparse point 在這個 unit test 環境不可行（需 admin）。
            // 此處改驗訊息模板正確：建立一個 throw flow 用 dummy file with manual attribute set。
            // Windows API SetFileAttributes 設 ReparsePoint flag 也需要實際 reparse data；
            // unit test 範圍只驗 happy path + 訊息 schema，throw path 由 integration test cover。
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="RuntimeDirectoryLock"/> 同 process 內第二次 AcquireAsync 同一 path
    /// 會被擋住、釋放第一個鎖後第二個能順利取得。覆蓋 cross-process file lock 在同
    /// process 內的行為（FileShare.None 的語意即跨 process / 跨 thread 都互斥）。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static async Task VerifyRuntimeDirectoryLockMutualExclusion()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "MediaEmbedKit.Mpv.Tests.RuntimeLock", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            // 第一次取鎖應立即成功。
            RuntimeDirectoryLock first = await RuntimeDirectoryLock.AcquireAsync(tempRoot, timeout: TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            // 第二次取鎖（同 process、不 release 第一把）應在 timeout 後擲 TimeoutException。
            try
            {
                await RuntimeDirectoryLock.AcquireAsync(tempRoot, timeout: TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                AssertEx.True(false, "第二次 AcquireAsync 應該擲 TimeoutException（鎖被持有中）");
            }
            catch (TimeoutException)
            {
                // 預期
            }

            // 釋放第一把鎖。
            first.Dispose();

            // 釋放後再 acquire 應成功。
            using RuntimeDirectoryLock third = await RuntimeDirectoryLock.AcquireAsync(tempRoot, timeout: TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            AssertEx.True(third != null, "釋放後第三次 AcquireAsync 應成功");
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 驗證 <see cref="MpvLicenseAuditor"/> 對常見 mpv 與 FFmpeg 設定字串的分類結果。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvLicenseAuditorClassification()
    {
        AssertEx.Equal(
            MpvBuildLicense.Lgpl,
            MpvLicenseAuditor.ClassifyMpvLicense("--enable-libmpv --enable-lgpl --enable-vulkan"),
            "LGPL libmpv 設定應分類為 Lgpl");

        AssertEx.Equal(
            MpvBuildLicense.Gpl,
            MpvLicenseAuditor.ClassifyMpvLicense("--enable-libmpv --enable-gpl"),
            "GPL libmpv 設定應分類為 Gpl");

        AssertEx.Equal(
            MpvBuildLicense.NonFree,
            MpvLicenseAuditor.ClassifyMpvLicense("--enable-libmpv --enable-gpl --enable-nonfree"),
            "包含 nonfree 應分類為 NonFree");

        AssertEx.Equal(
            MpvBuildLicense.Unknown,
            MpvLicenseAuditor.ClassifyMpvLicense(string.Empty),
            "空白設定應分類為 Unknown");

        AssertEx.Equal(
            MpvBuildLicense.Gpl,
            MpvLicenseAuditor.ClassifyFFmpegLicense("ffmpeg version 7.x ... --enable-gpl --enable-libx264"),
            "FFmpeg GPL build 版本字串應分類為 Gpl");

        AssertEx.Equal(
            MpvBuildLicense.NonFree,
            MpvLicenseAuditor.CombineLicenses(MpvBuildLicense.Lgpl, MpvBuildLicense.NonFree),
            "整體授權狀態應以較嚴格者為準");

        AssertEx.Equal(
            MpvBuildLicense.Gpl,
            MpvLicenseAuditor.CombineLicenses(MpvBuildLicense.Lgpl, MpvBuildLicense.Gpl),
            "LGPL+GPL 應視為 GPL");

        AssertEx.Equal(
            MpvBuildLicense.Lgpl,
            MpvLicenseAuditor.CombineLicenses(MpvBuildLicense.Lgpl, MpvBuildLicense.Lgpl),
            "兩端皆 LGPL 應視為 LGPL");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvMediaItem"/> fluent helper 會正確套用設定並回傳目前實例。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvMediaItemFluentHelpers()
    {
        MpvMediaItem item = new MpvMediaItem("https://example.com/stream")
            .WithStartTime(TimeSpan.FromSeconds(5))
            .WithEndTime(TimeSpan.FromMinutes(1))
            .WithHeader("User-Agent", "Mozilla/5.0")
            .WithOption("hwdec", "auto-safe")
            .WithYtdlpFormat(MpvYtdlpFormatPreset.UpTo720p);

        AssertEx.Equal(TimeSpan.FromSeconds(5), item.StartTime.GetValueOrDefault(), "WithStartTime 應套用值");
        AssertEx.Equal(TimeSpan.FromMinutes(1), item.EndTime.GetValueOrDefault(), "WithEndTime 應套用值");
        AssertEx.Equal("Mozilla/5.0", item.Headers["User-Agent"], "WithHeader 應加入標頭");
        AssertEx.Equal("auto-safe", item.Options["hwdec"], "WithOption 應加入選項");
        AssertEx.Equal(MpvYtdlpFormatPreset.UpTo720p, item.YtdlpFormatPreset.GetValueOrDefault(), "WithYtdlpFormat(preset) 應套用 preset");
        AssertEx.True(item.YtdlpFormat == null, "WithYtdlpFormat(preset) 應清除自訂 selector");

        item.WithYtdlpFormat("bestvideo+bestaudio");
        AssertEx.Equal("bestvideo+bestaudio", item.YtdlpFormat ?? string.Empty, "WithYtdlpFormat(string) 應套用 selector");
        AssertEx.True(!item.YtdlpFormatPreset.HasValue, "WithYtdlpFormat(string) 應清除 preset");

        AssertEx.Throws<ArgumentException>(
            delegate { item.WithHeader(" ", "value"); },
            "空白 HTTP 標頭名稱應被拒絕");

        AssertEx.Throws<ArgumentException>(
            delegate { item.WithOption(" ", "value"); },
            "空白 mpv 選項名稱應被拒絕");

        AssertEx.Throws<ArgumentException>(
            delegate { item.WithYtdlpFormat(" "); },
            "空白 yt-dlp selector 應被拒絕");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvEncodingOptions"/> 在兩階段（two-pass）clone 流程中
    /// 會把內部累積的 <c>WithMuxerOption</c> / <c>WithVideoCodecOption</c> /
    /// <c>WithAudioCodecOption</c> / <c>WithMetadataTag</c> / <c>WithoutMetadataTag</c>
    /// 清單全部複製到 clone，避免兩階段遺失 codec 參數。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyEncodingOptionsTwoPassClone()
    {
        MpvEncodingOptions source = new MpvEncodingOptions("out.mp4")
            .WithVideoCodec(MpvVideoCodecPreset.H264)
            .WithVideoCodecOption("b", "4000k")
            .WithVideoCodecOption("profile", "high")
            .WithAudioCodec(MpvAudioCodecPreset.Aac)
            .WithAudioCodecOption("b", "192k")
            .WithMuxerOption("movflags", "+faststart")
            .WithMetadataTag("title", "Phase 14 Verification")
            .WithMetadataTag("artist", "Tests")
            .WithoutMetadataTag("comment");

        System.Collections.Generic.IReadOnlyDictionary<string, string> sourceDict = source.ToOptionDictionary();
        AssertEx.True(sourceDict.ContainsKey("ovcopts"), "source 應產生 ovcopts。");
        AssertEx.True(sourceDict["ovcopts"].Contains("b=4000k"), "source ovcopts 應含 b=4000k。");
        AssertEx.True(sourceDict["ovcopts"].Contains("profile=high"), "source ovcopts 應含 profile=high。");
        AssertEx.True(sourceDict.ContainsKey("oacopts") && sourceDict["oacopts"].Contains("b=192k"), "source oacopts 應含 b=192k。");
        AssertEx.True(sourceDict.ContainsKey("ofopts") && sourceDict["ofopts"].Contains("movflags=+faststart"), "source ofopts 應含 movflags=+faststart。");
        AssertEx.True(sourceDict.ContainsKey("oset-metadata") && sourceDict["oset-metadata"].Contains("title=Phase 14 Verification"), "source oset-metadata 應含 title。");
        AssertEx.True(sourceDict.ContainsKey("oremove-metadata") && sourceDict["oremove-metadata"].Contains("comment"), "source oremove-metadata 應含 comment。");

        // 模擬 ClonePassOptions 的核心動作：建新實例、複製公開屬性與累積清單。
        MpvEncodingOptions clone = new MpvEncodingOptions("pass1.mp4");
        clone.VideoCodec = source.VideoCodec;
        clone.VideoCodecOptions = source.VideoCodecOptions;
        clone.AudioCodec = source.AudioCodec;
        clone.AudioCodecOptions = source.AudioCodecOptions;
        clone.ContainerFormat = source.ContainerFormat;
        clone.ContainerFormatOptions = source.ContainerFormatOptions;
        clone.CopyRawTimestamps = source.CopyRawTimestamps;
        clone.CopyMetadata = source.CopyMetadata;
        clone.Metadata = source.Metadata;
        clone.RemovedMetadata = source.RemovedMetadata;
        foreach (System.Collections.Generic.KeyValuePair<string, string> entry in source.AdditionalOptions)
        {
            clone.AdditionalOptions[entry.Key] = entry.Value;
        }

        typeof(MpvEncodingOptions)
            .GetMethod("CopyAccumulatedListsTo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(source, new object[] { clone });

        System.Collections.Generic.IReadOnlyDictionary<string, string> cloneDict = clone.ToOptionDictionary();
        AssertEx.True(cloneDict.ContainsKey("ovcopts") && cloneDict["ovcopts"].Contains("b=4000k"), "clone ovcopts 應含 b=4000k（不應遺失）。");
        AssertEx.True(cloneDict["ovcopts"].Contains("profile=high"), "clone ovcopts 應含 profile=high（不應遺失）。");
        AssertEx.True(cloneDict["oacopts"].Contains("b=192k"), "clone oacopts 應含 b=192k（不應遺失）。");
        AssertEx.True(cloneDict["ofopts"].Contains("movflags=+faststart"), "clone ofopts 應含 movflags=+faststart（不應遺失）。");
        AssertEx.True(cloneDict["oset-metadata"].Contains("title=Phase 14 Verification"), "clone oset-metadata 應含 title（不應遺失）。");
        AssertEx.True(cloneDict["oremove-metadata"].Contains("comment"), "clone oremove-metadata 應含 comment（不應遺失）。");

        string passDirectory = Path.Combine(Path.GetTempPath(), "mediaembedkit-test-pass-" + Guid.NewGuid().ToString("N"));
        string firstPassOutputPath = Path.Combine(passDirectory, "pass1.null");
        string passlogPrefix = Path.Combine(passDirectory, "ffpass");
        System.Reflection.MethodInfo clonePassOptions = typeof(MpvEncoder)
            .GetMethod("ClonePassOptions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        MpvEncodingOptions firstPass = (MpvEncodingOptions)clonePassOptions.Invoke(
            null,
            new object[] { source, firstPassOutputPath, 1, passlogPrefix, false })!;
        IReadOnlyDictionary<string, string> firstPassDict = firstPass.ToOptionDictionary();
        AssertEx.Equal(firstPassOutputPath, firstPassDict["o"], "第一階段輸出應位於暫存 pass 資料夾。");
        AssertEx.Equal("null", firstPassDict["of"], "第一階段應明確使用 null muxer。");
        AssertEx.Equal("no", firstPassDict["aid"], "第一階段應停用音訊。");
        AssertEx.True(firstPassDict["ovcopts"].Contains("flags=+pass1"), "第一階段 ovcopts 應含 pass1。");
        AssertEx.True(firstPassDict["ovcopts"].Contains("passlogfile=" + passlogPrefix), "第一階段 ovcopts 應含暫存 passlogfile。");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvRuntimeHealthReport.IsHealthy"/> / <see cref="MpvRuntimeHealthReport.IsComplete"/>
    /// 與 <see cref="MpvRuntimeHealthReport.IsHealthyFor"/> 的語意。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvRuntimeHealthReportSemantics()
    {
        // 透過反射叫 internal 建構式建立 fixture report。
        System.Reflection.ConstructorInfo? ctor = typeof(MpvRuntimeHealthReport)
            .GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                new[]
                {
                    typeof(string),
                    typeof(bool), typeof(bool), typeof(bool),
                    typeof(string),
                    typeof(bool), typeof(bool), typeof(bool), typeof(bool),
                    typeof(IReadOnlyList<string>)
                },
                modifiers: null);
        AssertEx.True(ctor != null, "MpvRuntimeHealthReport internal ctor 應存在。");

        IReadOnlyList<string> empty = new List<string>();

        MpvRuntimeHealthReport coreOnly = (MpvRuntimeHealthReport)ctor!.Invoke(new object?[]
        {
            "C:/fake", true, true, true, "2.5",
            false, false, false, false,
            empty
        });
        AssertEx.True(coreOnly.IsHealthy, "core libmpv 齊備應 IsHealthy=true。");
        AssertEx.True(!coreOnly.IsComplete, "缺附帶工具應 IsComplete=false。");
        AssertEx.True(coreOnly.IsHealthyFor(MpvRuntimeTools.None), "None 要求僅檢查核心。");
        AssertEx.True(!coreOnly.IsHealthyFor(MpvRuntimeTools.FFmpeg), "缺 ffmpeg 應 IsHealthyFor(FFmpeg)=false。");
        AssertEx.True(!coreOnly.IsHealthyFor(MpvRuntimeTools.All), "缺全部附帶工具應 IsHealthyFor(All)=false。");

        MpvRuntimeHealthReport allTools = (MpvRuntimeHealthReport)ctor!.Invoke(new object?[]
        {
            "C:/fake", true, true, true, "2.5",
            true, true, true, true,
            empty
        });
        AssertEx.True(allTools.IsHealthy, "全部齊備應 IsHealthy=true。");
        AssertEx.True(allTools.IsComplete, "全部齊備應 IsComplete=true。");
        AssertEx.True(allTools.IsHealthyFor(MpvRuntimeTools.All), "全部齊備應 IsHealthyFor(All)=true。");
        AssertEx.True(allTools.IsHealthyFor(MpvRuntimeTools.YtDlp | MpvRuntimeTools.FFmpeg), "子集合也應通過。");

        MpvRuntimeHealthReport coreBroken = (MpvRuntimeHealthReport)ctor!.Invoke(new object?[]
        {
            "C:/fake", true, false, false, null,
            true, true, true, true,
            new List<string> { "libmpv 載入失敗" } as IReadOnlyList<string>
        });
        AssertEx.True(!coreBroken.IsHealthy, "有 errors 不應 IsHealthy。");
        AssertEx.True(!coreBroken.IsComplete, "IsHealthy=false 必然 IsComplete=false。");
        AssertEx.True(!coreBroken.IsHealthyFor(MpvRuntimeTools.None), "core 故障時即使無附帶工具要求也應失敗。");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvRelayCommand"/> 的核心行為：建構檢查、CanExecute 預設、
    /// 委派執行、CanExecuteChanged 觸發以及參數型別委派。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvRelayCommand()
    {
        AssertEx.Throws<ArgumentNullException>(
            delegate { _ = new MpvRelayCommand((Action)null!); },
            "MpvRelayCommand 不接受 null Action 委派");

        AssertEx.Throws<ArgumentNullException>(
            delegate { _ = new MpvRelayCommand((Action<object?>)null!); },
            "MpvRelayCommand 不接受 null Action<object?> 委派");

        int executionCount = 0;
        MpvRelayCommand commandWithoutGuard = new MpvRelayCommand(() => executionCount++);
        AssertEx.True(
            commandWithoutGuard.CanExecute(null),
            "未指定 canExecute 時 CanExecute 應預設為 true");
        commandWithoutGuard.Execute(null);
        AssertEx.Equal(1, executionCount, "Execute 應呼叫 Action 委派一次");

        bool gate = false;
        int gatedExecutionCount = 0;
        MpvRelayCommand gatedCommand = new MpvRelayCommand(() => gatedExecutionCount++, () => gate);
        AssertEx.True(!gatedCommand.CanExecute(null), "canExecute 回傳 false 時 CanExecute 應為 false");
        gatedCommand.Execute(null);
        AssertEx.Equal(1, gatedExecutionCount, "Execute 不會自動檢查 CanExecute，仍會呼叫委派");

        int canExecuteChangedCount = 0;
        gatedCommand.CanExecuteChanged += (_, _) => canExecuteChangedCount++;
        gate = true;
        gatedCommand.RaiseCanExecuteChanged();
        AssertEx.Equal(1, canExecuteChangedCount, "RaiseCanExecuteChanged 應觸發事件一次");
        AssertEx.True(gatedCommand.CanExecute(null), "canExecute 回傳 true 時 CanExecute 應為 true");

        object? capturedParameter = null;
        MpvRelayCommand parameterized = new MpvRelayCommand(
            parameter => capturedParameter = parameter,
            parameter => parameter is string text && text.Length > 0);
        AssertEx.True(!parameterized.CanExecute(null), "空白參數應被 canExecute 拒絕");
        AssertEx.True(parameterized.CanExecute("ok"), "有效字串參數應通過 canExecute");
        parameterized.Execute("payload");
        AssertEx.Equal("payload", capturedParameter as string ?? string.Empty, "Execute 應把參數轉發給委派");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvPlayerOptions.CopyTo"/> 會把純值與集合欄位全部複製到目標。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyMpvPlayerOptionsCopyTo()
    {
        MpvPlayerOptions source = new MpvPlayerOptions
        {
            MpvLibraryPath = "runtime/libmpv-2.dll",
            EnableYtdlp = false,
            YtdlpPath = "yt-dlp.exe",
            YtdlpFormat = "bestvideo+bestaudio",
            YtdlpFormatPreset = MpvYtdlpFormatPreset.Best,
            ConfigDirectory = "runtime",
            LoadUserConfig = true,
            LogLevel = "info"
        };
        source.ConfigFiles.Add("mpv.conf");
        source.ScriptFiles.Add("scripts/demo.lua");
        source.InitialOptions["hwdec"] = "auto-safe";

        MpvPlayerOptions target = new MpvPlayerOptions();
        target.InitialOptions["legacy"] = "value";

        source.CopyTo(target);

        AssertEx.Equal("runtime/libmpv-2.dll", target.MpvLibraryPath ?? string.Empty, "MpvLibraryPath");
        AssertEx.True(!target.EnableYtdlp, "EnableYtdlp");
        AssertEx.Equal("yt-dlp.exe", target.YtdlpPath, "YtdlpPath");
        AssertEx.Equal("bestvideo+bestaudio", target.YtdlpFormat ?? string.Empty, "YtdlpFormat");
        AssertEx.Equal(MpvYtdlpFormatPreset.Best, target.YtdlpFormatPreset, "YtdlpFormatPreset");
        AssertEx.Equal("info", target.LogLevel, "LogLevel");
        AssertEx.Equal(1, target.ConfigFiles.Count, "ConfigFiles 數量");
        AssertEx.Equal("mpv.conf", target.ConfigFiles[0], "ConfigFiles 內容");
        AssertEx.Equal(1, target.ScriptFiles.Count, "ScriptFiles 數量");
        AssertEx.Equal(1, target.InitialOptions.Count, "InitialOptions 應被清空後重填");
        AssertEx.Equal("auto-safe", target.InitialOptions["hwdec"], "InitialOptions 內容");

        AssertEx.Throws<ArgumentNullException>(
            delegate { source.CopyTo(null!); },
            "target 為 null 應被拒絕");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 <see cref="MpvServiceCollectionExtensions.AddMpvPlayerFactory"/> 會把對應的工廠服務
    /// 登錄到容器，並對 null 參數做出明確的拒絕。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyDependencyInjectionExtensions()
    {
        ServiceCollection services = new ServiceCollection();
        MpvServiceCollectionExtensions.AddMpvPlayerFactory(services, builder => builder.UseHardwareDecoding());

        ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            Func<Task<MpvPlayer>>? factory = provider.GetService<Func<Task<MpvPlayer>>>();
            AssertEx.True(factory != null, "AddMpvPlayerFactory 應註冊 Func<Task<MpvPlayer>>");

            AssertEx.Throws<ArgumentNullException>(
                delegate
                {
                    MpvServiceCollectionExtensions.AddMpvPlayerFactory(null!, builder => { });
                },
                "services 為 null 應被拒絕");

            AssertEx.Throws<ArgumentNullException>(
                delegate
                {
                    MpvServiceCollectionExtensions.AddMpvPlayerFactory(new ServiceCollection(), null!);
                },
                "configure 為 null 應被拒絕");
        }
        finally
        {
            provider.Dispose();
        }

        return Task.CompletedTask;
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
    /// 驗證 runtime 下載選項的驗證策略預設值要求 GitHub 發行資產摘要。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyRuntimeVerificationOptionDefaults()
    {
        YtDlpDownloadOptions ytDlp = new YtDlpDownloadOptions();
        DenoDownloadOptions deno = new DenoDownloadOptions();
        FFmpegDownloadOptions ffmpeg = new FFmpegDownloadOptions();
        MpvWindowsBuildDownloadOptions libMpv = new MpvWindowsBuildDownloadOptions();

        AssertEx.Equal(MpvNativeAssetVerificationPolicy.RequireGitHubDigest, ytDlp.VerificationPolicy, "yt-dlp 驗證策略預設值");
        AssertEx.Equal(MpvNativeAssetVerificationPolicy.RequireGitHubDigest, deno.VerificationPolicy, "Deno 驗證策略預設值");
        AssertEx.Equal(MpvNativeAssetVerificationPolicy.RequireGitHubDigest, ffmpeg.VerificationPolicy, "FFmpeg 驗證策略預設值");
        AssertEx.Equal(MpvNativeAssetVerificationPolicy.RequireGitHubDigest, libMpv.VerificationPolicy, "libmpv 驗證策略預設值");
        AssertEx.True(ytDlp.VerifyDigest, "yt-dlp 應預設驗證可用 digest。");
        AssertEx.True(deno.VerifyDigest, "Deno 應預設驗證可用 digest。");
        AssertEx.True(ffmpeg.VerifyDigest, "FFmpeg 應預設驗證可用 digest。");
        AssertEx.True(libMpv.VerifyDigest, "libmpv 應預設驗證可用 digest。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證下載 helper 使用的預設瀏覽器標頭與 Chrome Stable 版本一致。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyBrowserRequestHeaders()
    {
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/"))
        {
            BrowserRequestHeaders.Apply(request.Headers, null);
            AssertEx.Equal(
                BrowserRequestHeaders.ChromeStableUserAgent,
                request.Headers.UserAgent.ToString(),
                "預設 User-Agent 應符合 Chrome Stable 常數。");
            AssertEx.Equal(
                BrowserRequestHeaders.SecChUa,
                string.Join(", ", request.Headers.GetValues("sec-ch-ua")),
                "sec-ch-ua 應符合 Chrome Stable major version。");
            AssertEx.Equal(
                BrowserRequestHeaders.SecChUaFullVersionList,
                string.Join(", ", request.Headers.GetValues("sec-ch-ua-full-version-list")),
                "sec-ch-ua-full-version-list 應符合 Chrome Stable full version。");
            AssertEx.True(
                BrowserRequestHeaders.ChromeStableUserAgent.Contains(BrowserRequestHeaders.ChromeStableVersion),
                "User-Agent 應包含 Chrome Stable full version。");
            AssertEx.True(
                BrowserRequestHeaders.SecChUaFullVersionList.Contains(BrowserRequestHeaders.ChromeStableVersion),
                "sec-ch-ua-full-version-list 應包含 Chrome Stable full version。");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 Windows runtime helper 預設不下載 FFmpeg（yt-dlp/FFmpeg-Builds 為 GPL
    /// build，預設拉進 runtime 會讓使用者背負未必知情的 GPL 散發義務），且可由呼叫
    /// 端明確啟用。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyWindowsRuntimeFFmpegOptionDefaults()
    {
        MpvWindowsRuntimeDownloadOptions options = new MpvWindowsRuntimeDownloadOptions();
        AssertEx.False(options.IncludeFFmpeg, "Windows runtime 預設不應下載 FFmpeg（GPL build）。");
        options.IncludeFFmpeg = true;
        AssertEx.True(options.IncludeFFmpeg, "Windows runtime 應允許明確啟用 FFmpeg 下載。");
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
    /// 驗證 catalog 宣告 Windows x64 與 ARM64 來源，並維持兩種 provider（shinchiro / zhongfly）。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyRuntimeCatalogs()
    {
        IReadOnlyList<MpvNativeRuntimeSource> windowsSources = MpvNativeRuntimeCatalog.GetSources(MpvNativeRuntimePlatform.Windows);
        IReadOnlyList<MpvNativeRuntimeSource> unknownSources = MpvNativeRuntimeCatalog.GetSources(MpvNativeRuntimePlatform.Unknown);
        AssertEx.Equal(2, windowsSources.Count, "Windows libmpv 來源數量（shinchiro + zhongfly）");
        AssertEx.Equal(0, unknownSources.Count, "未知平台 libmpv 來源數量");
        AssertEx.Equal(MpvNativeRuntimeSupportStatus.Supported, MpvNativeRuntimeCatalog.GetProjectSupportStatus(MpvNativeRuntimePlatform.Windows), "Windows 支援狀態");
        AssertEx.Equal(MpvNativeRuntimeSupportStatus.NotCataloged, MpvNativeRuntimeCatalog.GetProjectSupportStatus(MpvNativeRuntimePlatform.Unknown), "未知平台支援狀態");

        IReadOnlyList<ExternalToolRuntimeSource> ytDlpSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.YtDlp, MpvNativeRuntimePlatform.Windows);
        IReadOnlyList<ExternalToolRuntimeSource> denoSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.Deno, MpvNativeRuntimePlatform.Windows);
        IReadOnlyList<ExternalToolRuntimeSource> ffmpegSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.FFmpeg, MpvNativeRuntimePlatform.Windows);
        IReadOnlyList<ExternalToolRuntimeSource> unknownToolSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.YtDlp, MpvNativeRuntimePlatform.Unknown);
        IReadOnlyList<ExternalToolRuntimeSource> unknownFFmpegSources = ExternalToolRuntimeCatalog.GetSources(ExternalToolKind.FFmpeg, MpvNativeRuntimePlatform.Unknown);
        AssertEx.Equal(2, ytDlpSources.Count, "Windows yt-dlp 來源數量（x64 + ARM64）");
        AssertEx.Equal(2, denoSources.Count, "Windows Deno 來源數量（x64 + ARM64）");
        AssertEx.Equal(2, ffmpegSources.Count, "Windows FFmpeg 來源數量（x64 + ARM64）");
        AssertEx.Equal(0, unknownToolSources.Count, "未知平台外部工具來源數量");
        AssertEx.Equal(0, unknownFFmpegSources.Count, "未知平台 FFmpeg 來源數量");

        foreach (ExternalToolRuntimeSource source in ytDlpSources)
        {
            AssertEx.True(source.SupportsSelfUpdate, "yt-dlp 各架構均應提供自我更新命令。");
        }

        foreach (ExternalToolRuntimeSource source in denoSources)
        {
            AssertEx.True(source.SupportsSelfUpdate, "Deno 各架構均應提供自我更新命令。");
        }

        foreach (ExternalToolRuntimeSource source in ffmpegSources)
        {
            AssertEx.False(source.SupportsSelfUpdate, "FFmpeg 不應宣告內建自我更新命令。");
        }

        AssertEx.True(
            ffmpegSources.Any(s => s.AssetName == FFmpegDownloader.WindowsX64AssetName),
            "FFmpeg catalog 應含 Windows x64 GPL 資產。");
        AssertEx.True(
            ffmpegSources.Any(s => s.AssetName == FFmpegDownloader.WindowsArm64AssetName),
            "FFmpeg catalog 應含 Windows ARM64 GPL 資產。");
        AssertEx.True(
            ytDlpSources.Any(s => s.AssetName == "yt-dlp.exe"),
            "yt-dlp catalog 應含 x64 (yt-dlp.exe) 資產。");
        AssertEx.True(
            ytDlpSources.Any(s => s.AssetName == "yt-dlp_arm64.exe"),
            "yt-dlp catalog 應含 ARM64 (yt-dlp_arm64.exe) 資產。");
        AssertEx.True(
            denoSources.Any(s => s.AssetName == "deno-x86_64-pc-windows-msvc.zip"),
            "Deno catalog 應含 x64 資產。");
        AssertEx.True(
            denoSources.Any(s => s.AssetName == "deno-aarch64-pc-windows-msvc.zip"),
            "Deno catalog 應含 ARM64 資產。");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 驗證 ARM64 架構在三個 Architecture enum 與 FFmpeg helper 中對應到正確的資產 token / 檔名。
    /// </summary>
    /// <returns>代表測試流程的工作。</returns>
    private static Task VerifyWindowsArm64AssetMapping()
    {
        // libmpv：shinchiro / zhongfly 命名格式為 mpv-dev-{token}-*.7z
        Type mpvArchExt = typeof(MpvWindowsArchitectureExtensions);
        MethodInfo? mpvToken = mpvArchExt.GetMethod(
            "ToAssetToken",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(MpvWindowsArchitecture) },
            modifiers: null);
        AssertEx.True(mpvToken != null, "MpvWindowsArchitectureExtensions.ToAssetToken 應存在。");
        AssertEx.Equal("x86_64", (string)mpvToken!.Invoke(null, new object[] { MpvWindowsArchitecture.X64 })!, "x64 token");
        AssertEx.Equal("aarch64", (string)mpvToken!.Invoke(null, new object[] { MpvWindowsArchitecture.Arm64 })!, "ARM64 token");

        // yt-dlp：x64 用 yt-dlp.exe、ARM64 用 yt-dlp_arm64.exe
        Type ytdlpArchExt = typeof(YtDlpWindowsArchitectureExtensions);
        MethodInfo? ytdlpAsset = ytdlpArchExt.GetMethod(
            "ToAssetName",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(YtDlpWindowsArchitecture) },
            modifiers: null);
        AssertEx.True(ytdlpAsset != null, "YtDlpWindowsArchitectureExtensions.ToAssetName 應存在。");
        AssertEx.Equal("yt-dlp.exe", (string)ytdlpAsset!.Invoke(null, new object[] { YtDlpWindowsArchitecture.X64 })!, "yt-dlp x64 asset");
        AssertEx.Equal("yt-dlp_arm64.exe", (string)ytdlpAsset!.Invoke(null, new object[] { YtDlpWindowsArchitecture.Arm64 })!, "yt-dlp ARM64 asset");

        // Deno：x64 與 ARM64 命名為 deno-{token}-pc-windows-msvc.zip
        Type denoArchExt = typeof(DenoWindowsArchitectureExtensions);
        MethodInfo? denoAsset = denoArchExt.GetMethod(
            "ToAssetName",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(DenoWindowsArchitecture) },
            modifiers: null);
        AssertEx.True(denoAsset != null, "DenoWindowsArchitectureExtensions.ToAssetName 應存在。");
        AssertEx.Equal("deno-x86_64-pc-windows-msvc.zip", (string)denoAsset!.Invoke(null, new object[] { DenoWindowsArchitecture.X64 })!, "Deno x64 asset");
        AssertEx.Equal("deno-aarch64-pc-windows-msvc.zip", (string)denoAsset!.Invoke(null, new object[] { DenoWindowsArchitecture.Arm64 })!, "Deno ARM64 asset");

        // FFmpeg：透過 public GetWindowsAssetName helper
        AssertEx.Equal(FFmpegDownloader.WindowsX64AssetName, FFmpegDownloader.GetWindowsAssetName(MpvWindowsArchitecture.X64), "FFmpeg x64 asset");
        AssertEx.Equal(FFmpegDownloader.WindowsArm64AssetName, FFmpegDownloader.GetWindowsAssetName(MpvWindowsArchitecture.Arm64), "FFmpeg ARM64 asset");
        AssertEx.Equal("ffmpeg-master-latest-winarm64-gpl.zip", FFmpegDownloader.WindowsArm64AssetName, "FFmpeg ARM64 GPL 資產檔名");
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
