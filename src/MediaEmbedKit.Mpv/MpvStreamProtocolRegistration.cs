using System;
using System.IO;
using System.Runtime.InteropServices;
using MediaEmbedKit.Mpv.Native;

namespace MediaEmbedKit.Mpv
{
    /// <summary>
    /// 保存 libmpv 自訂串流通訊協定註冊所需的受控狀態。
    /// </summary>
    internal sealed class MpvStreamProtocolRegistration : IDisposable
    {
        /// <summary>
        /// 單次讀取配置的最大受控緩衝區大小。
        /// </summary>
        private const int MaximumReadBufferSize = 1024 * 1024;

        /// <summary>
        /// 開啟串流執行個體的受控委派。
        /// </summary>
        private readonly Func<string, Stream?> _openStream;

        /// <summary>
        /// 保存註冊物件的 GC 控制代碼，讓原生回呼可找回此物件。
        /// </summary>
        private readonly GCHandle _registrationHandle;

        /// <summary>
        /// 保存原生開啟回呼委派。
        /// </summary>
        private readonly MpvStreamOpenCallback _openCallback;

        /// <summary>
        /// 保存原生讀取回呼委派。
        /// </summary>
        private readonly MpvStreamReadCallback _readCallback;

        /// <summary>
        /// 保存原生搜尋回呼委派。
        /// </summary>
        private readonly MpvStreamSeekCallback _seekCallback;

        /// <summary>
        /// 保存原生大小查詢回呼委派。
        /// </summary>
        private readonly MpvStreamSizeCallback _sizeCallback;

        /// <summary>
        /// 保存原生關閉回呼委派。
        /// </summary>
        private readonly MpvStreamCloseCallback _closeCallback;

        /// <summary>
        /// 保存原生取消回呼委派。
        /// </summary>
        private readonly MpvStreamCancelCallback _cancelCallback;

        /// <summary>
        /// 表示此註冊物件是否已釋放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="MpvStreamProtocolRegistration"/> 類別的新執行個體。
        /// </summary>
        /// <param name="openStream">開啟串流執行個體的受控委派。</param>
        private MpvStreamProtocolRegistration(Func<string, Stream?> openStream)
        {
            _openStream = openStream;
            _openCallback = Open;
            _readCallback = Read;
            _seekCallback = Seek;
            _sizeCallback = Size;
            _closeCallback = Close;
            _cancelCallback = Cancel;
            _registrationHandle = GCHandle.Alloc(this);
        }

        /// <summary>
        /// 建立並註冊 libmpv 自訂唯讀串流通訊協定。
        /// </summary>
        /// <param name="player">要註冊通訊協定的 mpv 播放器。</param>
        /// <param name="protocol">不含 <c>://</c> 的通訊協定前置詞。</param>
        /// <param name="openStream">開啟串流執行個體的受控委派。</param>
        /// <returns>保存註冊狀態的物件。</returns>
        public static MpvStreamProtocolRegistration Register(MpvPlayer player, string protocol, Func<string, Stream?> openStream)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (string.IsNullOrWhiteSpace(protocol))
            {
                throw new ArgumentException("通訊協定前置詞不可空白。", nameof(protocol));
            }

            if (protocol.Contains("://"))
            {
                throw new ArgumentException("通訊協定前置詞不可包含 ://。", nameof(protocol));
            }

            if (openStream == null)
            {
                throw new ArgumentNullException(nameof(openStream));
            }

            MpvStreamProtocolRegistration registration = new MpvStreamProtocolRegistration(openStream);
            try
            {
                using (Utf8String protocolName = new Utf8String(protocol))
                {
                    IntPtr userData = GCHandle.ToIntPtr(registration._registrationHandle);
                    player.RegisterStreamProtocolCore(protocolName.Pointer, userData, registration._openCallback);
                }

                return registration;
            }
            catch
            {
                registration.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 釋放註冊物件保存的 GC 控制代碼。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_registrationHandle.IsAllocated)
            {
                _registrationHandle.Free();
            }

            GC.KeepAlive(_openCallback);
            GC.KeepAlive(_readCallback);
            GC.KeepAlive(_seekCallback);
            GC.KeepAlive(_sizeCallback);
            GC.KeepAlive(_closeCallback);
            GC.KeepAlive(_cancelCallback);
        }

        /// <summary>
        /// 建立單一 libmpv 串流執行個體。
        /// </summary>
        /// <param name="userData">註冊通訊協定時提供的使用者資料指標。</param>
        /// <param name="uriPointer">libmpv 要開啟的 URI 指標。</param>
        /// <param name="infoPointer">接收串流回呼資訊的結構指標。</param>
        /// <returns>成功時為 0；拒絕開啟或發生錯誤時為負值。</returns>
        private int Open(IntPtr userData, IntPtr uriPointer, IntPtr infoPointer)
        {
            if (_disposed || infoPointer == IntPtr.Zero)
            {
                return -1;
            }

            string uri = Utf8StringMarshaller.PtrToString(uriPointer) ?? string.Empty;
            Stream? stream = null;
            GCHandle streamHandle = default;
            try
            {
                stream = _openStream(uri);
                if (stream == null || !stream.CanRead)
                {
                    stream?.Dispose();
                    return -1;
                }

                streamHandle = GCHandle.Alloc(stream);
                MpvStreamCallbackInfo info = new MpvStreamCallbackInfo
                {
                    Cookie = GCHandle.ToIntPtr(streamHandle),
                    ReadCallback = Marshal.GetFunctionPointerForDelegate(_readCallback),
                    SeekCallback = Marshal.GetFunctionPointerForDelegate(_seekCallback),
                    SizeCallback = Marshal.GetFunctionPointerForDelegate(_sizeCallback),
                    CloseCallback = Marshal.GetFunctionPointerForDelegate(_closeCallback),
                    CancelCallback = Marshal.GetFunctionPointerForDelegate(_cancelCallback)
                };

                Marshal.StructureToPtr(info, infoPointer, false);
                return 0;
            }
            catch
            {
                if (streamHandle.IsAllocated)
                {
                    streamHandle.Free();
                }

                stream?.Dispose();
                return -1;
            }
        }

        /// <summary>
        /// 從受控串流讀取資料到 libmpv 緩衝區。
        /// </summary>
        /// <param name="cookie">串流執行個體的 GC 控制代碼指標。</param>
        /// <param name="buffer">接收讀取資料的原生緩衝區。</param>
        /// <param name="byteCount">要求讀取的最大位元組數。</param>
        /// <returns>實際讀取的位元組數；發生錯誤時為負值。</returns>
        private static long Read(IntPtr cookie, IntPtr buffer, ulong byteCount)
        {
            if (buffer == IntPtr.Zero || byteCount == 0)
            {
                return 0;
            }

            try
            {
                Stream? stream = GetStream(cookie);
                if (stream == null)
                {
                    return -1;
                }

                int count = (int)Math.Min(byteCount, (ulong)MaximumReadBufferSize);
                byte[] managedBuffer = new byte[count];
                int read = stream.Read(managedBuffer, 0, count);
                if (read > 0)
                {
                    Marshal.Copy(managedBuffer, 0, buffer, read);
                }

                return read;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 將受控串流移動到指定位置。
        /// </summary>
        /// <param name="cookie">串流執行個體的 GC 控制代碼指標。</param>
        /// <param name="offset">要移動到的絕對位元組位置。</param>
        /// <returns>搜尋後的絕對位元組位置；不支援搜尋或發生錯誤時為負值。</returns>
        private static long Seek(IntPtr cookie, long offset)
        {
            try
            {
                Stream? stream = GetStream(cookie);
                if (stream == null || !stream.CanSeek)
                {
                    return -1;
                }

                return stream.Seek(offset, SeekOrigin.Begin);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 取得受控串流的總長度。
        /// </summary>
        /// <param name="cookie">串流執行個體的 GC 控制代碼指標。</param>
        /// <returns>串流總位元組數；無法取得大小時為負值。</returns>
        private static long Size(IntPtr cookie)
        {
            try
            {
                Stream? stream = GetStream(cookie);
                if (stream == null || !stream.CanSeek)
                {
                    return -1;
                }

                return stream.Length;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// 關閉受控串流並釋放串流控制代碼。
        /// </summary>
        /// <param name="cookie">串流執行個體的 GC 控制代碼指標。</param>
        private static void Close(IntPtr cookie)
        {
            if (cookie == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle = default;
            try
            {
                handle = GCHandle.FromIntPtr(cookie);
                if (handle.Target is Stream stream)
                {
                    stream.Dispose();
                }
            }
            catch
            {
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }

        /// <summary>
        /// 處理 libmpv 串流取消要求。
        /// </summary>
        /// <param name="cookie">串流執行個體的 GC 控制代碼指標。</param>
        private static void Cancel(IntPtr cookie)
        {
            try
            {
                Stream? stream = GetStream(cookie);
                if (stream is IMpvStreamCancellationHandler cancellationHandler)
                {
                    cancellationHandler.CancelPendingRead();
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 從原生 cookie 取回受控串流。
        /// </summary>
        /// <param name="cookie">串流執行個體的 GC 控制代碼指標。</param>
        /// <returns>受控串流；控制代碼無效時為 <see langword="null"/>。</returns>
        private static Stream? GetStream(IntPtr cookie)
        {
            if (cookie == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return GCHandle.FromIntPtr(cookie).Target as Stream;
            }
            catch
            {
                return null;
            }
        }
    }
}
