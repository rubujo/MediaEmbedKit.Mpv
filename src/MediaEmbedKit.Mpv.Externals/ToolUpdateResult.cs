namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 表示外部工具自我更新命令的執行結果。
/// </summary>
public sealed class ToolUpdateResult
{
    /// <summary>
    /// 初始化 <see cref="ToolUpdateResult"/> 類別的新執行個體。
    /// </summary>
    /// <param name="executablePath">已執行的工具可執行檔路徑。</param>
    /// <param name="arguments">傳給工具的命令列引數。</param>
    /// <param name="exitCode">工具處理序結束代碼。</param>
    /// <param name="standardOutput">工具處理序標準輸出內容。</param>
    /// <param name="standardError">工具處理序標準錯誤內容。</param>
    internal ToolUpdateResult(string executablePath, string arguments, int exitCode, string standardOutput, string standardError)
    {
        ExecutablePath = executablePath;
        Arguments = arguments;
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>
    /// 取得已執行的工具可執行檔路徑。
    /// </summary>
    /// <value>工具可執行檔路徑。</value>
    public string ExecutablePath { get; private set; }

    /// <summary>
    /// 取得傳給工具的命令列引數。
    /// </summary>
    /// <value>命令列引數文字。</value>
    public string Arguments { get; private set; }

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
    /// 取得工具處理序是否成功完成。
    /// </summary>
    /// <value>結束代碼為 0 時為 <see langword="true"/>。</value>
    public bool Succeeded
    {
        get { return ExitCode == 0; }
    }
}
