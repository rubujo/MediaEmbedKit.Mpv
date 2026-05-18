using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaEmbedKit.Mpv.Downloads;

/// <summary>
/// Runtime 資料夾的 cross-process 互斥鎖，包覆 install / stage / apply / prune 序列，
/// 避免兩個應用實例 / 並行 CI 共用同一 runtime 資料夾並發寫壞 libmpv-2.dll。
/// </summary>
/// <remarks>
/// <para>
/// 透過在 <c>runtimeDirectory/.lock</c> 上 open 一個 <see cref="FileStream"/> with
/// <see cref="FileShare.None"/> 取得 lock；OS 層保證跨 process 互斥。Dispose 時釋放。
/// 用 <see cref="FileOptions.DeleteOnClose"/> 讓 lock 檔在 process crash / 正常結束時
/// 自動消失（避免使用者疑惑「為什麼有個 .lock 檔」）。
/// </para>
/// <para>
/// <strong>非 recursive：同 process 內若已持有此 lock，再次 acquire 同一 path 會 deadlock</strong>。
/// 呼叫端必須確保只在 outermost public entry point 取一次，nested helper 不再取。
/// 同 process 內進一步的執行緒互斥由各自 helper 的 in-process lock 處理
/// （例如 <c>MpvLibraryUpdateScheduler._syncRoot</c>）。
/// </para>
/// <para>
/// 預期競爭情境是「使用者偶爾兩個 process 同時 startup」，所以等待 timeout 設較長
/// （5 分鐘）—— libmpv 下載解壓本就可能耗時 30 秒+，正常持鎖時間不會太久。
/// </para>
/// </remarks>
internal sealed class RuntimeDirectoryLock : IDisposable
{
    /// <summary>Lock 檔的固定檔名。</summary>
    public const string LockFileName = ".lock";

    /// <summary>預設 acquire 等待 timeout（5 分鐘）。</summary>
    public static readonly TimeSpan DefaultAcquireTimeout = TimeSpan.FromMinutes(5);

    /// <summary>持鎖期間的 FileStream；Dispose 時自動 close 並刪檔。</summary>
    private FileStream? _stream;

    private RuntimeDirectoryLock(FileStream stream)
    {
        _stream = stream;
    }

    /// <summary>
    /// 在指定 runtime 資料夾上取得 cross-process 互斥鎖。被其他 process 占用時等待，
    /// 超過 timeout 擲 <see cref="TimeoutException"/>。
    /// </summary>
    /// <param name="runtimeDirectory">runtime 資料夾路徑。</param>
    /// <param name="timeout">等待 timeout；未指定時用 <see cref="DefaultAcquireTimeout"/>。</param>
    /// <param name="cancellationToken">可取消等待的 token。</param>
    /// <returns>取得的鎖；釋放鎖請 Dispose。</returns>
    /// <exception cref="TimeoutException">等待逾時。</exception>
    public static async Task<RuntimeDirectoryLock> AcquireAsync(
        string runtimeDirectory,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("runtime 資料夾不可為空白。", nameof(runtimeDirectory));
        }

        Directory.CreateDirectory(runtimeDirectory);
        string lockPath = Path.Combine(runtimeDirectory, LockFileName);
        TimeSpan effectiveTimeout = timeout ?? DefaultAcquireTimeout;
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(effectiveTimeout);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                FileStream stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.DeleteOnClose);
                return new RuntimeDirectoryLock(stream);
            }
            catch (IOException)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        "等待 runtime 資料夾鎖（" + lockPath + "）逾時 " + effectiveTimeout +
                        "；可能有另一 process 正在執行 install / update。");
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        "等待 runtime 資料夾鎖（" + lockPath + "）逾時 " + effectiveTimeout +
                        "（權限拒絕，可能是檔案被防毒鎖住）。");
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>釋放 cross-process lock —— 關閉 FileStream，配合 <see cref="FileOptions.DeleteOnClose"/>
    /// 自動刪除 <c>.lock</c> 檔。Dispose 失敗（檔案被其他 process 鎖等異常情境）會吞掉。</summary>
    public void Dispose()
    {
        if (_stream != null)
        {
            try
            {
                _stream.Dispose();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _stream = null;
        }
    }
}
