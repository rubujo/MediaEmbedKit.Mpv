using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Native
{
    /// <summary>
    /// 表示 libmpv 自訂串流讀取回呼。
    /// </summary>
    /// <param name="cookie">識別串流執行個體的使用者資料指標。</param>
    /// <param name="buffer">接收串流位元組的目標緩衝區指標。</param>
    /// <param name="byteCount">要求讀取的最大位元組數。</param>
    /// <returns>實際讀取的位元組數；發生錯誤時為負值。</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate long MpvStreamReadCallback(IntPtr cookie, IntPtr buffer, ulong byteCount);

    /// <summary>
    /// 表示 libmpv 自訂串流搜尋回呼。
    /// </summary>
    /// <param name="cookie">識別串流執行個體的使用者資料指標。</param>
    /// <param name="offset">要移動到的絕對位元組位置。</param>
    /// <returns>搜尋後的絕對位元組位置；不支援搜尋或發生錯誤時為負值。</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate long MpvStreamSeekCallback(IntPtr cookie, long offset);

    /// <summary>
    /// 表示 libmpv 自訂串流大小查詢回呼。
    /// </summary>
    /// <param name="cookie">識別串流執行個體的使用者資料指標。</param>
    /// <returns>串流總位元組數；無法取得大小時為負值。</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate long MpvStreamSizeCallback(IntPtr cookie);

    /// <summary>
    /// 表示 libmpv 自訂串流關閉回呼。
    /// </summary>
    /// <param name="cookie">識別串流執行個體的使用者資料指標。</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MpvStreamCloseCallback(IntPtr cookie);

    /// <summary>
    /// 表示 libmpv 自訂串流取消回呼。
    /// </summary>
    /// <param name="cookie">識別串流執行個體的使用者資料指標。</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MpvStreamCancelCallback(IntPtr cookie);

    /// <summary>
    /// 表示 libmpv 自訂唯讀串流開啟回呼。
    /// </summary>
    /// <param name="userData">註冊通訊協定時提供的使用者資料指標。</param>
    /// <param name="uri">libmpv 要開啟的 URI 指標。</param>
    /// <param name="info">接收串流回呼資訊的結構指標。</param>
    /// <returns>成功時為 0；拒絕開啟或發生錯誤時為負值。</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int MpvStreamOpenCallback(IntPtr userData, IntPtr uri, IntPtr info);

    /// <summary>
    /// 對應 libmpv <c>mpv_stream_cb_info</c> 結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvStreamCallbackInfo
    {
        /// <summary>
        /// 串流執行個體使用者資料指標。
        /// </summary>
        public IntPtr Cookie;

        /// <summary>
        /// 串流讀取回呼指標。
        /// </summary>
        public IntPtr ReadCallback;

        /// <summary>
        /// 串流搜尋回呼指標。
        /// </summary>
        public IntPtr SeekCallback;

        /// <summary>
        /// 串流大小查詢回呼指標。
        /// </summary>
        public IntPtr SizeCallback;

        /// <summary>
        /// 串流關閉回呼指標。
        /// </summary>
        public IntPtr CloseCallback;

        /// <summary>
        /// 串流取消回呼指標。
        /// </summary>
        public IntPtr CancelCallback;
    }
}
