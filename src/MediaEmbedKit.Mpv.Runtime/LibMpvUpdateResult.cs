using MediaEmbedKit.Mpv.Externals;

﻿namespace MediaEmbedKit.Mpv.Runtime;

/// <summary>
/// 表示 libmpv 執行階段更新作業的結果。
/// </summary>
public sealed class LibMpvUpdateResult
{
    /// <summary>
    /// 初始化 <see cref="LibMpvUpdateResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="runtimeDirectory">執行階段資料夾路徑。</param>
    /// <param name="targetLibraryPath">執行階段資料夾中的 libmpv 目標路徑。</param>
    /// <param name="updatedLibraryPath">本次更新產生的 libmpv 檔案路徑。</param>
    /// <param name="download">libmpv 下載結果。</param>
    /// <param name="appliedToRuntimeDirectory">更新是否已套用到執行階段資料夾。</param>
    /// <param name="requiresProcessRestart">更新是否需要重新啟動處理序。</param>
    /// <param name="restartMessage">重新啟動或套用更新的提示訊息。</param>
    internal LibMpvUpdateResult(
        string runtimeDirectory,
        string targetLibraryPath,
        string updatedLibraryPath,
        MpvWindowsBuildDownloadResult download,
        bool appliedToRuntimeDirectory,
        bool requiresProcessRestart,
        string restartMessage)
    {
        RuntimeDirectory = runtimeDirectory;
        TargetLibraryPath = targetLibraryPath;
        UpdatedLibraryPath = updatedLibraryPath;
        Download = download;
        AppliedToRuntimeDirectory = appliedToRuntimeDirectory;
        RequiresProcessRestart = requiresProcessRestart;
        RestartMessage = restartMessage;
    }

    /// <summary>
    /// 取得執行階段資料夾路徑。
    /// </summary>
    /// <value>執行階段資料夾路徑。</value>
    public string RuntimeDirectory { get; private set; }

    /// <summary>
    /// 取得執行階段資料夾中的 libmpv 目標路徑。
    /// </summary>
    /// <value>libmpv 目標檔案路徑。</value>
    public string TargetLibraryPath { get; private set; }

    /// <summary>
    /// 取得本次更新產生的 libmpv 檔案路徑。
    /// </summary>
    /// <value>已下載或已暫存的 libmpv 檔案路徑。</value>
    public string UpdatedLibraryPath { get; private set; }

    /// <summary>
    /// 取得執行階段資料夾中的 libmpv 目標路徑。
    /// </summary>
    /// <value>libmpv 目標檔案路徑。</value>
    public string LibraryPath
    {
        get { return TargetLibraryPath; }
    }

    /// <summary>
    /// 取得 libmpv 下載結果。
    /// </summary>
    /// <value>libmpv Windows 建置下載結果。</value>
    public MpvWindowsBuildDownloadResult Download { get; private set; }

    /// <summary>
    /// 取得更新是否已套用到執行階段資料夾。
    /// </summary>
    /// <value>已套用到執行階段資料夾時為 <see langword="true"/>。</value>
    public bool AppliedToRuntimeDirectory { get; private set; }

    /// <summary>
    /// 取得更新是否需要重新啟動處理序。
    /// </summary>
    /// <value>需要重新啟動處理序時為 <see langword="true"/>。</value>
    public bool RequiresProcessRestart { get; private set; }

    /// <summary>
    /// 取得重新啟動或套用更新的提示訊息。
    /// </summary>
    /// <value>更新後的提示訊息。</value>
    public string RestartMessage { get; private set; }
}
