using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// 提供直接執行 yt-dlp 並接收輸出事件的輔助類別。
/// </summary>
public sealed class YtDlpProcessRunner
{
    /// <summary>
    /// 實際執行外部工具的共用處理序執行器。
    /// </summary>
    private readonly ExternalToolProcessRunner _runner;

    /// <summary>
    /// 初始化 <see cref="YtDlpProcessRunner"/> 類別的新執行個體。
    /// </summary>
    /// <param name="executablePath">yt-dlp 可執行檔路徑。</param>
    public YtDlpProcessRunner(string executablePath)
    {
        _runner = new ExternalToolProcessRunner(executablePath);
    }

    /// <summary>
    /// 在 yt-dlp 輸出一列文字時發生。
    /// </summary>
    public event EventHandler<ExternalToolOutputEventArgs>? OutputReceived
    {
        add { _runner.OutputReceived += value; }
        remove { _runner.OutputReceived -= value; }
    }

    /// <summary>
    /// 取得 yt-dlp 可執行檔路徑。
    /// </summary>
    /// <value>yt-dlp 可執行檔路徑。</value>
    public string ExecutablePath
    {
        get { return _runner.ExecutablePath; }
    }

    /// <summary>
    /// 取得或設定 yt-dlp 工作目錄。
    /// </summary>
    /// <value>yt-dlp 工作目錄；未指定時使用目前處理序工作目錄。</value>
    public string? WorkingDirectory
    {
        get { return _runner.WorkingDirectory; }
        set { _runner.WorkingDirectory = value; }
    }

    /// <summary>
    /// 取得或設定預設等待 yt-dlp 完成的時間。
    /// </summary>
    /// <value>預設等待 yt-dlp 完成的時間。</value>
    public TimeSpan DefaultTimeout
    {
        get { return _runner.DefaultTimeout; }
        set { _runner.DefaultTimeout = value; }
    }

    /// <summary>
    /// 設定 yt-dlp 處理序的環境變數。
    /// </summary>
    /// <param name="name">環境變數名稱。</param>
    /// <param name="value">環境變數值。</param>
    public void SetEnvironmentVariable(string name, string value)
    {
        _runner.SetEnvironmentVariable(name, value);
    }

    /// <summary>
    /// 非同步執行 yt-dlp。
    /// </summary>
    /// <param name="arguments">要傳給 yt-dlp 的引數集合。</param>
    /// <param name="timeout">等待 yt-dlp 完成的時間；未指定時使用 <see cref="DefaultTimeout"/>。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 yt-dlp 執行結果的工作。</returns>
    public Task<ExternalToolProcessResult> RunAsync(
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return _runner.RunAsync(arguments, timeout, cancellationToken);
    }

    /// <summary>
    /// 取得指定媒體網址的格式清單。
    /// </summary>
    /// <param name="url">要分析的媒體網址。</param>
    /// <param name="timeout">等待 yt-dlp 完成的時間；未指定時使用 <see cref="DefaultTimeout"/>。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 yt-dlp 格式清單命令結果的工作。</returns>
    public Task<ExternalToolProcessResult> ListFormatsAsync(
        string url,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("媒體網址不可為空白。", nameof(url));
        }

        return RunAsync(new[] { "--no-warnings", "--list-formats", url }, timeout, cancellationToken);
    }

    /// <summary>
    /// 以單行 JSON 形式取得指定媒體網址的資訊。
    /// </summary>
    /// <param name="url">要分析的媒體網址。</param>
    /// <param name="timeout">等待 yt-dlp 完成的時間；未指定時使用 <see cref="DefaultTimeout"/>。</param>
    /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
    /// <returns>表示 yt-dlp 單行 JSON 命令結果的工作。</returns>
    public Task<ExternalToolProcessResult> DumpSingleJsonAsync(
        string url,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("媒體網址不可為空白。", nameof(url));
        }

        return RunAsync(new[] { "--no-warnings", "--dump-single-json", url }, timeout, cancellationToken);
    }

    /// <summary>
    /// 以 <see cref="IAsyncEnumerable{T}"/> 串流執行 yt-dlp。
    /// </summary>
    /// <param name="arguments">要傳給 yt-dlp 的引數集合。</param>
    /// <param name="cancellationToken">取消列舉的 token；取消時會嘗試終止處理序。</param>
    /// <returns>逐行回傳 yt-dlp 的輸出事件。</returns>
    public IAsyncEnumerable<ExternalToolOutputEventArgs> StreamAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return _runner.StreamAsync(arguments, cancellationToken);
    }

    /// <summary>
    /// 以 <see cref="IAsyncEnumerable{T}"/> 串流取得指定媒體網址的格式清單。
    /// </summary>
    /// <param name="url">要分析的媒體網址。</param>
    /// <param name="cancellationToken">取消列舉的 token。</param>
    /// <returns>逐行回傳 yt-dlp 格式清單命令的輸出事件。</returns>
    public IAsyncEnumerable<ExternalToolOutputEventArgs> StreamFormatsAsync(
        string url,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("媒體網址不可為空白。", nameof(url));
        }

        return _runner.StreamAsync(new[] { "--no-warnings", "--list-formats", url }, cancellationToken);
    }

    /// <summary>
    /// 建立明確指定 Deno 執行階段路徑時使用的 yt-dlp 引數值。
    /// </summary>
    /// <param name="denoPath">Deno 可執行檔路徑。</param>
    /// <returns>可傳給 <c>--js-runtimes</c> 的 Deno 引數值。</returns>
    public static string CreateDenoJavaScriptRuntimeArgument(string denoPath)
    {
        if (string.IsNullOrWhiteSpace(denoPath))
        {
            throw new ArgumentException("Deno 可執行檔路徑不可為空白。", nameof(denoPath));
        }

        return "deno:" + denoPath;
    }
}
