using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads
{
    /// <summary>
    /// 提供直接執行 Deno 並接收輸出事件的輔助類別。
    /// </summary>
    public sealed class DenoProcessRunner
    {
        /// <summary>
        /// 實際執行外部工具的共用處理序執行器。
        /// </summary>
        private readonly ExternalToolProcessRunner _runner;

        /// <summary>
        /// 初始化 <see cref="DenoProcessRunner"/> 類別的新執行個體。
        /// </summary>
        /// <param name="executablePath">Deno 可執行檔路徑。</param>
        public DenoProcessRunner(string executablePath)
        {
            _runner = new ExternalToolProcessRunner(executablePath);
        }

        /// <summary>
        /// 在 Deno 輸出一列文字時發生。
        /// </summary>
        public event EventHandler<ExternalToolOutputEventArgs>? OutputReceived
        {
            add { _runner.OutputReceived += value; }
            remove { _runner.OutputReceived -= value; }
        }

        /// <summary>
        /// 取得 Deno 可執行檔路徑。
        /// </summary>
        /// <value>Deno 可執行檔路徑。</value>
        public string ExecutablePath
        {
            get { return _runner.ExecutablePath; }
        }

        /// <summary>
        /// 取得或設定 Deno 工作目錄。
        /// </summary>
        /// <value>Deno 工作目錄；未指定時使用目前處理序工作目錄。</value>
        public string? WorkingDirectory
        {
            get { return _runner.WorkingDirectory; }
            set { _runner.WorkingDirectory = value; }
        }

        /// <summary>
        /// 取得或設定預設等待 Deno 完成的時間。
        /// </summary>
        /// <value>預設等待 Deno 完成的時間。</value>
        public TimeSpan DefaultTimeout
        {
            get { return _runner.DefaultTimeout; }
            set { _runner.DefaultTimeout = value; }
        }

        /// <summary>
        /// 非同步執行 Deno。
        /// </summary>
        /// <param name="arguments">要傳給 Deno 的引數集合。</param>
        /// <param name="timeout">等待 Deno 完成的時間；未指定時使用 <see cref="DefaultTimeout"/>。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示 Deno 執行結果的工作。</returns>
        public Task<ExternalToolProcessResult> RunAsync(
            IEnumerable<string> arguments,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return _runner.RunAsync(arguments, timeout, cancellationToken);
        }

        /// <summary>
        /// 取得 Deno 版本資訊。
        /// </summary>
        /// <param name="timeout">等待 Deno 完成的時間；未指定時使用 <see cref="DefaultTimeout"/>。</param>
        /// <param name="cancellationToken">可取消非同步作業的語彙基元。</param>
        /// <returns>表示 Deno 版本命令結果的工作。</returns>
        public Task<ExternalToolProcessResult> GetVersionAsync(
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return RunAsync(new[] { "--version" }, timeout, cancellationToken);
        }

        /// <summary>
        /// 以 <see cref="IAsyncEnumerable{T}"/> 串流執行 Deno。
        /// </summary>
        /// <param name="arguments">要傳給 Deno 的引數集合。</param>
        /// <param name="cancellationToken">取消列舉的 token；取消時會嘗試終止處理序。</param>
        /// <returns>逐行回傳 Deno 的輸出事件。</returns>
        public IAsyncEnumerable<ExternalToolOutputEventArgs> StreamAsync(
            IEnumerable<string> arguments,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return _runner.StreamAsync(arguments, cancellationToken);
        }

        /// <summary>
        /// 以 <see cref="IAsyncEnumerable{T}"/> 串流取得 Deno 版本資訊。
        /// </summary>
        /// <param name="cancellationToken">取消列舉的 token。</param>
        /// <returns>逐行回傳 Deno 版本命令的輸出事件。</returns>
        public IAsyncEnumerable<ExternalToolOutputEventArgs> StreamVersionAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return _runner.StreamAsync(new[] { "--version" }, cancellationToken);
        }
    }
}
