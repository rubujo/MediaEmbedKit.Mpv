using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 以事件方式執行外部工具並收集標準輸出與標準錯誤。
/// </summary>
public sealed class ExternalToolProcessRunner
{
    /// <summary>
    /// 預設等待外部工具完成的時間。
    /// </summary>
    private static readonly TimeSpan DefaultTimeoutValue = TimeSpan.FromMinutes(5);
    /// <summary>
    /// 外部工具結束後等待輸出資料流關閉的時間。
    /// </summary>
    private static readonly TimeSpan OutputCloseTimeoutValue = TimeSpan.FromSeconds(10);
    /// <summary>
    /// 已設定的環境變數。
    /// </summary>
    private readonly Dictionary<string, string> _environmentVariables;

    /// <summary>
    /// 初始化 <see cref="ExternalToolProcessRunner"/> 類別的新執行個體。
    /// </summary>
    /// <param name="executablePath">
    /// 外部工具可執行檔路徑。
    /// </param>
    public ExternalToolProcessRunner(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("外部工具可執行檔路徑不可為空白。", nameof(executablePath));
        }

        ExecutablePath = executablePath;
        DefaultTimeout = DefaultTimeoutValue;
        _environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 在外部工具輸出一列文字時發生。
    /// </summary>
    /// <remarks>
    /// 此事件由處理序輸出回呼執行緒引發。UI 應用程式接收後需自行切換到 UI 執行緒。
    /// </remarks>
    public event EventHandler<ExternalToolOutputEventArgs>? OutputReceived;

    /// <summary>
    /// 取得外部工具可執行檔路徑。
    /// </summary>
    /// <value>
    /// 外部工具可執行檔路徑。
    /// </value>
    public string ExecutablePath { get; private set; }

    /// <summary>
    /// 取得或設定外部工具工作目錄。
    /// </summary>
    /// <value>
    /// 外部工具工作目錄；未指定時使用目前處理序工作目錄。
    /// </value>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 取得或設定預設等待外部工具完成的時間。
    /// </summary>
    /// <value>
    /// 預設等待外部工具完成的時間。
    /// </value>
    public TimeSpan DefaultTimeout { get; set; }

    /// <summary>
    /// 設定外部工具處理序的環境變數。
    /// </summary>
    /// <param name="name">
    /// 環境變數名稱。
    /// </param>
    /// <param name="value">
    /// 環境變數值。
    /// </param>
    public void SetEnvironmentVariable(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("環境變數名稱不可為空白。", nameof(name));
        }

        _environmentVariables[name] = value ?? string.Empty;
    }

    /// <summary>
    /// 移除外部工具處理序的環境變數覆寫。
    /// </summary>
    /// <param name="name">
    /// 環境變數名稱。
    /// </param>
    /// <returns>
    /// 有移除既有覆寫時為 <see langword="true"/>。
    /// </returns>
    public bool RemoveEnvironmentVariable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("環境變數名稱不可為空白。", nameof(name));
        }

        return _environmentVariables.Remove(name);
    }

    /// <summary>
    /// 非同步執行外部工具。
    /// </summary>
    /// <param name="arguments">
    /// 要傳給外部工具的引數集合。
    /// </param>
    /// <param name="timeout">
    /// 等待工具完成的時間；未指定時使用 <see cref="DefaultTimeout"/>。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 表示外部工具執行結果的工作。
    /// </returns>
    public async Task<ExternalToolProcessResult> RunAsync(
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        List<string> argumentList = new List<string>(arguments);
        string argumentText = FormatArguments(argumentList);
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            Arguments = argumentText,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            startInfo.WorkingDirectory = WorkingDirectory!;
        }

        foreach (KeyValuePair<string, string> item in _environmentVariables)
        {
            startInfo.EnvironmentVariables[item.Key] = item.Value;
        }

        using (Process process = new Process())
        {
            StringBuilder standardOutput = new StringBuilder();
            StringBuilder standardError = new StringBuilder();
            object outputGate = new object();
            TaskCompletionSource<bool> outputClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> errorClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                HandleOutputLine(ExternalToolOutputStream.StandardOutput, e.Data, standardOutput, outputGate, outputClosed);
            };
            process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                HandleOutputLine(ExternalToolOutputStream.StandardError, e.Data, standardError, outputGate, errorClosed);
            };

            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            if (!process.Start())
            {
                throw new InvalidOperationException("無法啟動外部工具：" + ExecutablePath);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            bool exited;
            try
            {
                exited = await WaitForExitAsync(process, timeout ?? DefaultTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                KillProcess(process);
                throw;
            }

            if (!exited)
            {
                KillProcess(process);
                throw new TimeoutException(ExecutablePath + " 未在指定時間內結束。");
            }

            await WaitForOutputStreamsClosedAsync(outputClosed.Task, errorClosed.Task, cancellationToken).ConfigureAwait(false);
            DateTimeOffset completedAt = DateTimeOffset.UtcNow;
            return new ExternalToolProcessResult(
                ExecutablePath,
                argumentList,
                argumentText,
                process.ExitCode,
                standardOutput.ToString(),
                standardError.ToString(),
                startedAt,
                completedAt);
        }
    }

    /// <summary>
    /// 以 <see cref="IAsyncEnumerable{T}"/> 串流外部工具的標準輸出與標準錯誤輸出。
    /// </summary>
    /// <param name="arguments">
    /// 要傳給外部工具的引數集合。
    /// </param>
    /// <param name="cancellationToken">
    /// 取消列舉的 token；取消時會嘗試終止處理序。
    /// </param>
    /// <returns>
    /// 逐行回傳的輸出事件。
    /// </returns>
    public async IAsyncEnumerable<ExternalToolOutputEventArgs> StreamAsync(
        IEnumerable<string> arguments,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        List<string> argumentList = new List<string>(arguments);
        string argumentText = FormatArguments(argumentList);
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            Arguments = argumentText,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            startInfo.WorkingDirectory = WorkingDirectory!;
        }

        foreach (KeyValuePair<string, string> item in _environmentVariables)
        {
            startInfo.EnvironmentVariables[item.Key] = item.Value;
        }

        BlockingCollection<ExternalToolOutputEventArgs> buffer = new BlockingCollection<ExternalToolOutputEventArgs>(new ConcurrentQueue<ExternalToolOutputEventArgs>());
        using (Process process = new Process())
        {
            int pendingStreams = 2;
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                EnqueueStreamLine(buffer, ExternalToolOutputStream.StandardOutput, e.Data, ref pendingStreams);
            };
            process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                EnqueueStreamLine(buffer, ExternalToolOutputStream.StandardError, e.Data, ref pendingStreams);
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("無法啟動外部工具：" + ExecutablePath);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                while (true)
                {
                    ExternalToolOutputEventArgs item;
                    try
                    {
                        item = buffer.Take(cancellationToken);
                    }
                    catch (InvalidOperationException)
                    {
                        yield break;
                    }
                    catch (OperationCanceledException)
                    {
                        KillProcess(process);
                        throw;
                    }

                    yield return item;
                }
            }
            finally
            {
                if (!process.HasExited)
                {
                    KillProcess(process);
                }
            }
        }
    }

    /// <summary>
    /// 將處理序輸出回呼資料寫入串流緩衝。
    /// </summary>
    /// <param name="buffer">
    /// 輸出緩衝。
    /// </param>
    /// <param name="stream">
    /// 輸出資料流。
    /// </param>
    /// <param name="line">
    /// 回呼接收到的單列文字；資料流結束時為 <see langword="null"/>。
    /// </param>
    /// <param name="pendingStreams">
    /// 尚未結束的資料流數。
    /// </param>
    private static void EnqueueStreamLine(
        BlockingCollection<ExternalToolOutputEventArgs> buffer,
        ExternalToolOutputStream stream,
        string? line,
        ref int pendingStreams)
    {
        if (line == null)
        {
            if (Interlocked.Decrement(ref pendingStreams) <= 0)
            {
                buffer.CompleteAdding();
            }

            return;
        }

        try
        {
            buffer.Add(new ExternalToolOutputEventArgs(stream, line, DateTimeOffset.Now));
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// 將引數清單轉換為命令列文字。
    /// </summary>
    /// <param name="arguments">
    /// 要格式化的引數清單。
    /// </param>
    /// <returns>
    /// 命令列引數文字。
    /// </returns>
    public static string FormatArguments(IEnumerable<string> arguments)
    {
        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        StringBuilder builder = new StringBuilder();
        bool first = true;
        foreach (string argument in arguments)
        {
            if (!first)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteArgument(argument ?? string.Empty));
            first = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// 處理外部工具輸出的一列文字。
    /// </summary>
    /// <param name="stream">
    /// 輸出的資料流。
    /// </param>
    /// <param name="line">
    /// 輸出的單列文字；資料流結束時為 <see langword="null"/>。
    /// </param>
    /// <param name="builder">
    /// 累積輸出的字串建構器。
    /// </param>
    /// <param name="outputGate">
    /// 同步輸出累積的物件。
    /// </param>
    /// <param name="closed">
    /// 資料流結束工作。
    /// </param>
    private void HandleOutputLine(
        ExternalToolOutputStream stream,
        string? line,
        StringBuilder builder,
        object outputGate,
        TaskCompletionSource<bool> closed)
    {
        if (line == null)
        {
            closed.TrySetResult(true);
            return;
        }

        lock (outputGate)
        {
            builder.AppendLine(line);
        }

        OutputReceived?.Invoke(this, new ExternalToolOutputEventArgs(stream, line, DateTimeOffset.Now));
    }

    /// <summary>
    /// 非同步等待處理序結束並套用逾時限制。
    /// </summary>
    /// <param name="process">
    /// 要等待的處理序。
    /// </param>
    /// <param name="timeout">
    /// 等待處理序完成的逾時時間。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 處理序在逾時前結束時為 <see langword="true"/>。
    /// </returns>
    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (!process.HasExited)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// 等待標準輸出與標準錯誤資料流完成關閉。
    /// </summary>
    /// <param name="outputClosed">
    /// 標準輸出完成關閉的工作。
    /// </param>
    /// <param name="errorClosed">
    /// 標準錯誤完成關閉的工作。
    /// </param>
    /// <param name="cancellationToken">
    /// 可取消非同步作業的語彙基元。
    /// </param>
    /// <returns>
    /// 代表等待輸出資料流關閉流程的工作。
    /// </returns>
    private static async Task WaitForOutputStreamsClosedAsync(
        Task outputClosed,
        Task errorClosed,
        CancellationToken cancellationToken)
    {
        Task closedTask = Task.WhenAll(outputClosed, errorClosed);
        Task delayTask = Task.Delay(OutputCloseTimeoutValue, cancellationToken);
        Task completedTask = await Task.WhenAny(closedTask, delayTask).ConfigureAwait(false);
        if (completedTask != closedTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("外部工具結束後，輸出資料流未在指定時間內關閉。");
        }

        await closedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// 嘗試終止處理序；委派到 <see cref="DownloadUtility.TryKillProcess"/>，後者會吞各種
    /// platform 例外、.NET 5+ 路徑同時 kill 整個子處理序樹。
    /// </summary>
    /// <param name="process">
    /// 要終止的處理序。
    /// </param>
    private static void KillProcess(Process process)
    {
        DownloadUtility.TryKillProcess(process);
    }

    /// <summary>
    /// 將單一引數轉換為可放入命令列的文字。
    /// </summary>
    /// <param name="argument">
    /// 要格式化的引數。
    /// </param>
    /// <returns>
    /// 已加上必要引號與逸出的引數文字。
    /// </returns>
    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        bool needsQuotes = argument.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) >= 0;
        if (!needsQuotes)
        {
            return argument;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append('"');
        int backslashCount = 0;
        for (int i = 0; i < argument.Length; i++)
        {
            char character = argument[i];
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount);
                backslashCount = 0;
            }

            builder.Append(character);
        }

        if (backslashCount > 0)
        {
            builder.Append('\\', backslashCount * 2);
        }

        builder.Append('"');
        return builder.ToString();
    }
}
