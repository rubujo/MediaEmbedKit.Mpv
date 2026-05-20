using System;
using System.Threading;
using MediaEmbedKit.Mpv.Native;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 管理由 libmpv 建立的額外用戶端控制代碼。
/// </summary>
public sealed class MpvClientHandle : IDisposable
{
    /// <summary>
    /// 保存額外 libmpv 用戶端的原生控制代碼。
    /// </summary>
    private IntPtr _handle;

    /// <summary>
    /// 表示釋放時是否要求 libmpv 終止播放核心。
    /// </summary>
    private readonly bool _terminateOnDispose;

    /// <summary>
    /// 初始化 <see cref="MpvClientHandle"/> 類別的新執行個體。
    /// </summary>
    /// <param name="handle">
    /// 要管理的 libmpv 用戶端控制代碼。
    /// </param>
    /// <param name="terminateOnDispose">
    /// 釋放時是否呼叫終止銷毀函式。
    /// </param>
    internal MpvClientHandle(IntPtr handle, bool terminateOnDispose)
    {
        if (handle == IntPtr.Zero)
        {
            throw new ArgumentException("libmpv 用戶端控制代碼不可為零。", nameof(handle));
        }

        _handle = handle;
        _terminateOnDispose = terminateOnDispose;
    }

    /// <summary>
    /// 取得目前是否已釋放此用戶端控制代碼。
    /// </summary>
    /// <value>
    /// 已釋放時為 <see langword="true"/>。
    /// </value>
    public bool IsDisposed
    {
        get { return _handle == IntPtr.Zero; }
    }

    /// <summary>
    /// 取得額外 libmpv 用戶端的原生控制代碼。
    /// </summary>
    /// <value>
    /// 額外 libmpv 用戶端的原生控制代碼。
    /// </value>
    public IntPtr DangerousHandle
    {
        get
        {
            IntPtr handle = _handle;
            if (handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return handle;
        }
    }

    /// <summary>
    /// 釋放額外 libmpv 用戶端控制代碼。
    /// </summary>
    public void Dispose()
    {
        IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (_terminateOnDispose)
        {
            MpvNative.mpv_terminate_destroy(handle);
        }
        else
        {
            MpvNative.mpv_destroy(handle);
        }
    }
}
