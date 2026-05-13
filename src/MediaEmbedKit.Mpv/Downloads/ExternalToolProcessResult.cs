using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 表示外部工具處理序執行完成後的結果。
/// </summary>
public sealed class ExternalToolProcessResult
{
    /// <summary>
    /// 初始化 <see cref="ExternalToolProcessResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="executablePath">已執行的工具可執行檔路徑。</param>
    /// <param name="arguments">傳給工具的引數清單。</param>
    /// <param name="argumentText">傳給工具的命令列引數文字。</param>
    /// <param name="exitCode">工具處理序結束代碼。</param>
    /// <param name="standardOutput">工具處理序標準輸出內容。</param>
    /// <param name="standardError">工具處理序標準錯誤內容。</param>
    /// <param name="startedAt">工具處理序啟動時間。</param>
    /// <param name="completedAt">工具處理序完成時間。</param>
    internal ExternalToolProcessResult(
        string executablePath,
        IReadOnlyList<string> arguments,
        string argumentText,
        int exitCode,
        string standardOutput,
        string standardError,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        ExecutablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        Arguments = new ReadOnlyCollection<string>(new List<string>(arguments ?? throw new ArgumentNullException(nameof(arguments))));
        ArgumentText = argumentText ?? string.Empty;
        ExitCode = exitCode;
        StandardOutput = standardOutput ?? string.Empty;
        StandardError = standardError ?? string.Empty;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// 取得已執行的工具可執行檔路徑。
    /// </summary>
    /// <value>工具可執行檔路徑。</value>
    public string ExecutablePath { get; private set; }

    /// <summary>
    /// 取得傳給工具的引數清單。
    /// </summary>
    /// <value>傳給工具的引數清單。</value>
    public IReadOnlyList<string> Arguments { get; private set; }

    /// <summary>
    /// 取得傳給工具的命令列引數文字。
    /// </summary>
    /// <value>命令列引數文字。</value>
    public string ArgumentText { get; private set; }

    /// <summary>
    /// 取得工具處理序結束代碼。
    /// </summary>
    /// <value>工具處理序結束代碼。</value>
    public int ExitCode { get; private set; }

    /// <summary>
    /// 取得工具處理序標準輸出內容。
    /// </summary>
    /// <value>標準輸出內容。</value>
    public string StandardOutput { get; private set; }

    /// <summary>
    /// 取得工具處理序標準錯誤內容。
    /// </summary>
    /// <value>標準錯誤內容。</value>
    public string StandardError { get; private set; }

    /// <summary>
    /// 取得工具處理序啟動時間。
    /// </summary>
    /// <value>工具處理序啟動時間。</value>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>
    /// 取得工具處理序完成時間。
    /// </summary>
    /// <value>工具處理序完成時間。</value>
    public DateTimeOffset CompletedAt { get; private set; }

    /// <summary>
    /// 取得工具處理序執行時間。
    /// </summary>
    /// <value>工具處理序執行時間。</value>
    public TimeSpan Duration
    {
        get { return CompletedAt - StartedAt; }
    }

    /// <summary>
    /// 取得工具處理序是否成功完成。
    /// </summary>
    /// <value>結束代碼為 0 時為 <see langword="true"/>。</value>
    public bool Succeeded
    {
        get { return ExitCode == 0; }
    }
}
