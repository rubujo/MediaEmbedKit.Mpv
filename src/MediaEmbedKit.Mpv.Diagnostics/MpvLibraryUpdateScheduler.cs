using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供 libmpv-2.dll 的暫存、套用與回滾流程封裝。
/// 整體規則：libmpv 載入後不能在同處理序熱替換；本 scheduler 將更新暫存於
/// <c>.updates/&lt;timestamp&gt;/libmpv-2.dll</c>，並在下次啟動且 libmpv 尚未載入時
/// 將前一版搬到 <c>.previous/libmpv-2.dll</c>、暫存版本提升為使用版本。
/// </summary>
public sealed class MpvLibraryUpdateScheduler
{
    /// <summary>
    /// libmpv 在執行階段資料夾中的固定檔案名稱。
    /// </summary>
    private const string LibMpvFileName = "libmpv-2.dll";
    /// <summary>
    /// 暫存子資料夾名稱，與 <see cref="MpvWindowsRuntimeInstaller"/> 對齊。
    /// </summary>
    private const string UpdatesDirectoryName = ".updates";
    /// <summary>
    /// 前一版備份的子資料夾名稱。
    /// </summary>
    private const string PreviousDirectoryName = ".previous";

    /// <summary>
    /// 執行階段資料夾完整路徑。
    /// </summary>
    private readonly string _runtimeDirectory;
    /// <summary>
    /// 預設使用的 Windows libmpv 建置下載選項。
    /// </summary>
    private readonly MpvWindowsBuildDownloadOptions _defaultOptions;

    /// <summary>
    /// 同處理序內 apply / rollback / prune 互斥鎖 —— 防 multi-thread startup race
    /// （IsLoaded 檢查與 File.Copy 之間 TOCTOU；同時跑 apply 與 prune 互踩 staged dir 等）。
    /// 此 lock 只防同處理序；跨 process race 由 runtimeDirectory file lock 處理。
    /// </summary>
    private readonly object _syncRoot = new object();

    /// <summary>
    /// 初始化 <see cref="MpvLibraryUpdateScheduler"/> 類別的新執行個體。
    /// </summary>
    /// <param name="runtimeDirectory">執行階段資料夾，必須是日後 libmpv-2.dll 載入位置的根目錄。</param>
    /// <param name="options">下載 Windows libmpv 建置時的預設選項；未提供時使用預設值。</param>
    public MpvLibraryUpdateScheduler(string runtimeDirectory, MpvWindowsBuildDownloadOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("執行階段資料夾不可為空白。", nameof(runtimeDirectory));
        }

        _runtimeDirectory = Path.GetFullPath(runtimeDirectory);
        _defaultOptions = options ?? new MpvWindowsBuildDownloadOptions();
    }

    /// <summary>
    /// 取得執行階段資料夾完整路徑。
    /// </summary>
    /// <value>執行階段資料夾完整路徑。</value>
    public string RuntimeDirectory
    {
        get { return _runtimeDirectory; }
    }

    /// <summary>
    /// 取得目前使用的 libmpv-2.dll 路徑。
    /// </summary>
    /// <value>libmpv-2.dll 完整路徑。</value>
    public string CurrentLibraryPath
    {
        get { return Path.Combine(_runtimeDirectory, LibMpvFileName); }
    }

    /// <summary>
    /// 取得前一版 libmpv-2.dll 備份路徑。
    /// </summary>
    /// <value><c>.previous/libmpv-2.dll</c> 完整路徑。</value>
    public string PreviousLibraryPath
    {
        get { return Path.Combine(_runtimeDirectory, PreviousDirectoryName, LibMpvFileName); }
    }

    /// <summary>
    /// 取得暫存資料夾完整路徑。
    /// </summary>
    /// <value><c>.updates/</c> 完整路徑。</value>
    public string StagedRootDirectory
    {
        get { return Path.Combine(_runtimeDirectory, UpdatesDirectoryName); }
    }

    /// <summary>
    /// 列出目前已暫存的 libmpv 更新，依時間戳記由舊到新排序。
    /// </summary>
    /// <returns>已暫存的更新集合；目前沒有暫存時集合為空。</returns>
    public IReadOnlyList<MpvLibraryStagedUpdate> ListStagedUpdates()
    {
        string root = StagedRootDirectory;
        if (!Directory.Exists(root))
        {
            return Array.Empty<MpvLibraryStagedUpdate>();
        }

        string[] candidates = Directory.GetDirectories(root);
        Array.Sort(candidates, StringComparer.Ordinal);
        List<MpvLibraryStagedUpdate> updates = new List<MpvLibraryStagedUpdate>(candidates.Length);
        foreach (string candidate in candidates)
        {
            string libraryPath = Path.Combine(candidate, LibMpvFileName);
            if (File.Exists(libraryPath))
            {
                updates.Add(new MpvLibraryStagedUpdate(candidate, libraryPath, Directory.GetCreationTimeUtc(candidate)));
            }
        }

        return new ReadOnlyCollection<MpvLibraryStagedUpdate>(updates);
    }

    /// <summary>
    /// 下載最新 libmpv 建置並暫存到 <c>.updates/&lt;timestamp&gt;/</c>。
    /// 若 libmpv 尚未在當前處理序載入，會同時把暫存版本提升為使用版本。
    /// </summary>
    /// <param name="cancellationToken">取消下載與暫存的 token。</param>
    /// <returns>本次暫存的結果。</returns>
    public Task<MpvLibraryStageResult> StageAsync(CancellationToken cancellationToken = default)
    {
        return StageAsync(_defaultOptions, cancellationToken);
    }

    /// <summary>
    /// 下載最新 libmpv 建置並暫存到 <c>.updates/&lt;timestamp&gt;/</c>。
    /// </summary>
    /// <param name="options">本次下載要使用的選項；未提供時使用建構時提供的預設選項。</param>
    /// <param name="cancellationToken">取消下載與暫存的 token。</param>
    /// <returns>本次暫存的結果。</returns>
    public async Task<MpvLibraryStageResult> StageAsync(
        MpvWindowsBuildDownloadOptions? options,
        CancellationToken cancellationToken = default)
    {
        MpvWindowsBuildDownloadOptions effective = options ?? _defaultOptions;
        LibMpvUpdateResult result = await MpvWindowsRuntimeInstaller.UpdateLibMpvAsync(
            _runtimeDirectory,
            effective,
            cancellationToken).ConfigureAwait(false);

        return new MpvLibraryStageResult(
            _runtimeDirectory,
            result.UpdatedLibraryPath,
            result.LibraryPath,
            result.RequiresProcessRestart,
            result.RequiresProcessRestart,
            result.Download);
    }

    /// <summary>
    /// 在 libmpv 尚未載入時，把最近一次暫存的更新提升為使用版本，
    /// 並將先前的 libmpv-2.dll 備份到 <c>.previous/</c>。
    /// </summary>
    /// <returns>套用結果；無暫存可套用時 <see cref="MpvLibraryApplyResult.Applied"/> 為 <see langword="false"/>。</returns>
    public MpvLibraryApplyResult ApplyStagedOnStartup()
    {
        lock (_syncRoot)
        {
            // 在 lock 內再驗一次 IsLoaded：原本 IsLoaded 檢查與 File.Copy 之間的 TOCTOU
            // 可讓另一執行緒 Load 進來 → File.Copy 撞鎖檔 IOException。
            if (MpvLibraryLoader.IsLoaded)
            {
                throw new InvalidOperationException("libmpv 已在當前處理序載入，無法套用暫存的更新；請於下次啟動前呼叫。");
            }

            IReadOnlyList<MpvLibraryStagedUpdate> staged = ListStagedUpdates();
            if (staged.Count == 0)
            {
                return new MpvLibraryApplyResult(false, null, null, "沒有可套用的暫存更新。");
            }

            MpvLibraryStagedUpdate latest = staged[staged.Count - 1];
            string currentPath = CurrentLibraryPath;
            string previousDirectory = Path.Combine(_runtimeDirectory, PreviousDirectoryName);
            Directory.CreateDirectory(previousDirectory);
            string previousPath = PreviousLibraryPath;
            if (File.Exists(currentPath))
            {
                File.Copy(currentPath, previousPath, true);
            }

            // File.Copy 前再驗一次 IsLoaded —— lock 阻擋同處理序 race，但若多執行緒在
            // lock 取得後別處 Load（不該發生但保險）會在這裡明確 throw 而非沉默成功。
            if (MpvLibraryLoader.IsLoaded)
            {
                throw new InvalidOperationException("libmpv 已在 apply 過程中被載入，套用中止以避免覆寫鎖檔；請於下次啟動前重試。");
            }

            File.Copy(latest.LibraryPath, currentPath, true);

            try
            {
                Directory.Delete(latest.StagedDirectory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return new MpvLibraryApplyResult(true, latest.LibraryPath, previousPath, "已將暫存版本提升為使用版本。");
        }
    }

    /// <summary>
    /// 將 <c>.previous/libmpv-2.dll</c> 還原為使用版本。
    /// 此操作要求 libmpv 尚未在當前處理序載入。
    /// </summary>
    /// <returns>還原結果；找不到 <c>.previous/</c> 時 <see cref="MpvLibraryRollbackResult.RolledBack"/> 為 <see langword="false"/>。</returns>
    public MpvLibraryRollbackResult Rollback()
    {
        lock (_syncRoot)
        {
            if (MpvLibraryLoader.IsLoaded)
            {
                throw new InvalidOperationException("libmpv 已在當前處理序載入，無法回滾；請於下次啟動前呼叫。");
            }

            string previousPath = PreviousLibraryPath;
            if (!File.Exists(previousPath))
            {
                return new MpvLibraryRollbackResult(false, null, "找不到 .previous/libmpv-2.dll，無法回滾。");
            }

            string currentPath = CurrentLibraryPath;

            // File.Copy 前再驗一次 IsLoaded（同 ApplyStagedOnStartup 的 TOCTOU 防護）。
            if (MpvLibraryLoader.IsLoaded)
            {
                throw new InvalidOperationException("libmpv 已在 rollback 過程中被載入，回滾中止以避免覆寫鎖檔；請於下次啟動前重試。");
            }

            File.Copy(previousPath, currentPath, true);
            try
            {
                File.Delete(previousPath);
                string previousDirectory = Path.Combine(_runtimeDirectory, PreviousDirectoryName);
                if (Directory.Exists(previousDirectory) && !Directory.EnumerateFileSystemEntries(previousDirectory).Any())
                {
                    Directory.Delete(previousDirectory, false);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return new MpvLibraryRollbackResult(true, currentPath, "已從 .previous/ 還原為使用版本。");
        }
    }

    /// <summary>
    /// 清除 <c>.updates/</c> 內所有尚未套用的暫存。
    /// </summary>
    /// <returns>已被清除的暫存集合。</returns>
    public IReadOnlyList<MpvLibraryStagedUpdate> PruneStagedUpdates()
    {
        lock (_syncRoot)
        {
            IReadOnlyList<MpvLibraryStagedUpdate> staged = ListStagedUpdates();
            foreach (MpvLibraryStagedUpdate update in staged)
            {
                try
                {
                    Directory.Delete(update.StagedDirectory, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return staged;
        }
    }
}

/// <summary>
/// 描述一個暫存的 libmpv 更新。
/// </summary>
public sealed class MpvLibraryStagedUpdate
{
    /// <summary>
    /// 初始化 <see cref="MpvLibraryStagedUpdate"/> 類別的新執行個體。
    /// </summary>
    /// <param name="stagedDirectory">暫存資料夾完整路徑。</param>
    /// <param name="libraryPath">暫存的 libmpv-2.dll 完整路徑。</param>
    /// <param name="stagedAtUtc">暫存建立時間（UTC）。</param>
    internal MpvLibraryStagedUpdate(string stagedDirectory, string libraryPath, DateTime stagedAtUtc)
    {
        StagedDirectory = stagedDirectory;
        LibraryPath = libraryPath;
        StagedAtUtc = stagedAtUtc;
    }

    /// <summary>
    /// 取得暫存資料夾完整路徑。
    /// </summary>
    /// <value>暫存資料夾完整路徑。</value>
    public string StagedDirectory { get; }

    /// <summary>
    /// 取得暫存的 libmpv-2.dll 完整路徑。
    /// </summary>
    /// <value>暫存的 libmpv-2.dll 完整路徑。</value>
    public string LibraryPath { get; }

    /// <summary>
    /// 取得暫存建立時間（UTC）。
    /// </summary>
    /// <value>暫存建立時間。</value>
    public DateTime StagedAtUtc { get; }
}

/// <summary>
/// 描述一次 <see cref="MpvLibraryUpdateScheduler.StageAsync(CancellationToken)"/> 的結果。
/// </summary>
public sealed class MpvLibraryStageResult
{
    /// <summary>
    /// 初始化 <see cref="MpvLibraryStageResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="runtimeDirectory">執行階段資料夾。</param>
    /// <param name="stagedLibraryPath">暫存或已套用的 libmpv-2.dll 路徑。</param>
    /// <param name="currentLibraryPath">執行階段資料夾中目前的 libmpv-2.dll 路徑。</param>
    /// <param name="requiresProcessRestart">是否需要重新啟動處理序才能套用更新。</param>
    /// <param name="libraryAlreadyLoaded">libmpv 是否已在當前處理序載入。</param>
    /// <param name="build">本次下載的 build 結果，可用於審計。</param>
    internal MpvLibraryStageResult(
        string runtimeDirectory,
        string stagedLibraryPath,
        string currentLibraryPath,
        bool requiresProcessRestart,
        bool libraryAlreadyLoaded,
        MpvWindowsBuildDownloadResult build)
    {
        RuntimeDirectory = runtimeDirectory;
        StagedLibraryPath = stagedLibraryPath;
        CurrentLibraryPath = currentLibraryPath;
        RequiresProcessRestart = requiresProcessRestart;
        LibraryAlreadyLoaded = libraryAlreadyLoaded;
        Build = build;
    }

    /// <summary>
    /// 取得執行階段資料夾。
    /// </summary>
    /// <value>執行階段資料夾完整路徑。</value>
    public string RuntimeDirectory { get; }

    /// <summary>
    /// 取得暫存或已套用的 libmpv-2.dll 路徑。
    /// </summary>
    /// <value>libmpv-2.dll 完整路徑。</value>
    public string StagedLibraryPath { get; }

    /// <summary>
    /// 取得執行階段資料夾中目前的 libmpv-2.dll 路徑。
    /// </summary>
    /// <value>libmpv-2.dll 完整路徑。</value>
    public string CurrentLibraryPath { get; }

    /// <summary>
    /// 取得是否需要重新啟動處理序才能套用更新。
    /// </summary>
    /// <value>需要重新啟動時為 <see langword="true"/>。</value>
    public bool RequiresProcessRestart { get; }

    /// <summary>
    /// 取得 libmpv 是否已在當前處理序載入。
    /// </summary>
    /// <value>已載入時為 <see langword="true"/>。</value>
    public bool LibraryAlreadyLoaded { get; }

    /// <summary>
    /// 取得本次下載的 build 結果。
    /// </summary>
    /// <value>下載結果，可用於審計與紀錄。</value>
    public MpvWindowsBuildDownloadResult Build { get; }
}

/// <summary>
/// 描述一次 <see cref="MpvLibraryUpdateScheduler.ApplyStagedOnStartup"/> 的結果。
/// </summary>
public sealed class MpvLibraryApplyResult
{
    /// <summary>
    /// 初始化 <see cref="MpvLibraryApplyResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="applied">是否真的把暫存更新提升為使用版本。</param>
    /// <param name="sourceLibraryPath">本次套用所使用的暫存 libmpv-2.dll 路徑。</param>
    /// <param name="previousLibraryPath">本次套用前所備份的 libmpv-2.dll 路徑。</param>
    /// <param name="message">套用結果的補充說明。</param>
    internal MpvLibraryApplyResult(bool applied, string? sourceLibraryPath, string? previousLibraryPath, string message)
    {
        Applied = applied;
        SourceLibraryPath = sourceLibraryPath;
        PreviousLibraryPath = previousLibraryPath;
        Message = message;
    }

    /// <summary>
    /// 取得是否真的把暫存更新提升為使用版本。
    /// </summary>
    /// <value>有套用時為 <see langword="true"/>。</value>
    public bool Applied { get; }

    /// <summary>
    /// 取得本次套用所使用的暫存 libmpv-2.dll 路徑。
    /// </summary>
    /// <value>暫存路徑；未套用時為 <see langword="null"/>。</value>
    public string? SourceLibraryPath { get; }

    /// <summary>
    /// 取得本次套用前所備份的 libmpv-2.dll 路徑。
    /// </summary>
    /// <value>備份路徑；未套用時為 <see langword="null"/>。</value>
    public string? PreviousLibraryPath { get; }

    /// <summary>
    /// 取得套用結果的補充說明。
    /// </summary>
    /// <value>說明文字。</value>
    public string Message { get; }
}

/// <summary>
/// 描述一次 <see cref="MpvLibraryUpdateScheduler.Rollback"/> 的結果。
/// </summary>
public sealed class MpvLibraryRollbackResult
{
    /// <summary>
    /// 初始化 <see cref="MpvLibraryRollbackResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="rolledBack">是否真的從 <c>.previous/</c> 還原為使用版本。</param>
    /// <param name="restoredLibraryPath">本次還原後的 libmpv-2.dll 路徑。</param>
    /// <param name="message">回滾結果的補充說明。</param>
    internal MpvLibraryRollbackResult(bool rolledBack, string? restoredLibraryPath, string message)
    {
        RolledBack = rolledBack;
        RestoredLibraryPath = restoredLibraryPath;
        Message = message;
    }

    /// <summary>
    /// 取得是否真的從 <c>.previous/</c> 還原為使用版本。
    /// </summary>
    /// <value>有還原時為 <see langword="true"/>。</value>
    public bool RolledBack { get; }

    /// <summary>
    /// 取得本次還原後的 libmpv-2.dll 路徑。
    /// </summary>
    /// <value>還原後的路徑；未還原時為 <see langword="null"/>。</value>
    public string? RestoredLibraryPath { get; }

    /// <summary>
    /// 取得回滾結果的補充說明。
    /// </summary>
    /// <value>說明文字。</value>
    public string Message { get; }
}
