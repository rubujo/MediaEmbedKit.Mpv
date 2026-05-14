using System;
using System.Runtime.InteropServices;
using MediaEmbedKit.Mpv.Native;

namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 封裝 libmpv software render API 內容。
/// </summary>
public sealed class MpvSoftwareRenderContext : IDisposable
{
    /// <summary>
    /// 保存 libmpv 更新通知委派，避免遭到記憶體回收。
    /// </summary>
    private readonly MpvRenderUpdateCallback _updateCallback;
    /// <summary>
    /// libmpv render API 內容指標。
    /// </summary>
    private IntPtr _context;
    /// <summary>
    /// 表示目前 render API 內容是否已釋放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="MpvSoftwareRenderContext"/> 類別的新執行個體。
    /// </summary>
    /// <param name="context">libmpv render API 內容指標。</param>
    private MpvSoftwareRenderContext(IntPtr context)
    {
        _context = context;
        _updateCallback = OnRenderUpdate;
        MpvNative.mpv_render_context_set_update_callback(_context, _updateCallback, IntPtr.Zero);
    }

    /// <summary>
    /// 在 libmpv 通知 render API 有更新可處理時發生。
    /// </summary>
    public event EventHandler? UpdateAvailable;

    /// <summary>
    /// 取得目前 render API 內容是否已釋放。
    /// </summary>
    /// <value>已釋放時為 <see langword="true"/>。</value>
    public bool IsDisposed
    {
        get { return _disposed; }
    }

    /// <summary>
    /// 取得 libmpv render API 內容的原生控制代碼。
    /// </summary>
    /// <value>libmpv render API 內容指標。</value>
    public IntPtr DangerousHandle
    {
        get
        {
            EnsureNotDisposed();
            return _context;
        }
    }

    /// <summary>
    /// 為指定播放器建立 software render API 內容。
    /// </summary>
    /// <param name="player">要關聯的 mpv 播放器。</param>
    /// <returns>新建立的 software render API 內容。</returns>
    public static MpvSoftwareRenderContext Create(MpvPlayer player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        using (Utf8String apiType = new Utf8String("sw"))
        {
            MpvRenderParam[] parameters = new[]
            {
                new MpvRenderParam(MpvRenderParamType.ApiType, apiType.Pointer),
                MpvRenderParam.Terminator
            };

            IntPtr context = player.CreateRenderContext(parameters);
            return new MpvSoftwareRenderContext(context);
        }
    }

    /// <summary>
    /// 讀取並清除 libmpv render API 的更新旗標。
    /// </summary>
    /// <returns>目前待處理的 render API 更新旗標。</returns>
    public MpvRenderUpdateFlags Update()
    {
        EnsureNotDisposed();
        return (MpvRenderUpdateFlags)MpvNative.mpv_render_context_update(_context);
    }

    /// <summary>
    /// 將 mpv 目前影格繪製到指定的軟體像素緩衝區。
    /// </summary>
    /// <param name="buffer">目標像素緩衝區指標。</param>
    /// <param name="width">目標表面寬度。</param>
    /// <param name="height">目標表面高度。</param>
    /// <param name="stride">每列位元組數。</param>
    /// <param name="format">mpv software render 像素格式。</param>
    /// <param name="blockForTargetTime">是否等待影格目標時間。</param>
    /// <param name="skipRendering">是否略過實際繪製並只推進 render API 影格狀態。</param>
    public void Render(
        IntPtr buffer,
        int width,
        int height,
        int stride,
        string format = "bgr0",
        bool blockForTargetTime = true,
        bool skipRendering = false)
    {
        EnsureNotDisposed();
        if (buffer == IntPtr.Zero)
        {
            throw new ArgumentException("像素緩衝區指標不可為零。", nameof(buffer));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "寬度必須大於零。");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "高度必須大於零。");
        }

        if (format == null)
        {
            throw new ArgumentNullException(nameof(format));
        }

        int minimumStride = GetMinimumStride(width, format);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(nameof(stride), "步幅必須可容納整列像素。");
        }

        IntPtr sizePointer = IntPtr.Zero;
        IntPtr stridePointer = IntPtr.Zero;
        IntPtr blockPointer = IntPtr.Zero;
        IntPtr skipPointer = IntPtr.Zero;
        try
        {
            sizePointer = Marshal.AllocHGlobal(sizeof(int) * 2);
            Marshal.WriteInt32(sizePointer, 0, width);
            Marshal.WriteInt32(sizePointer, sizeof(int), height);
            stridePointer = AllocSizeT(stride);
            blockPointer = AllocInt32(blockForTargetTime ? 1 : 0);
            skipPointer = AllocInt32(skipRendering ? 1 : 0);

            using (Utf8String formatString = new Utf8String(format))
            {
                MpvRenderParam[] parameters = new[]
                {
                    new MpvRenderParam(MpvRenderParamType.SoftwareSize, sizePointer),
                    new MpvRenderParam(MpvRenderParamType.SoftwareFormat, formatString.Pointer),
                    new MpvRenderParam(MpvRenderParamType.SoftwareStride, stridePointer),
                    new MpvRenderParam(MpvRenderParamType.SoftwarePointer, buffer),
                    new MpvRenderParam(MpvRenderParamType.BlockForTargetTime, blockPointer),
                    new MpvRenderParam(MpvRenderParamType.SkipRendering, skipPointer),
                    MpvRenderParam.Terminator
                };

                MpvError.ThrowIfError(MpvNative.mpv_render_context_render(_context, parameters));
            }
        }
        finally
        {
            Free(sizePointer);
            Free(stridePointer);
            Free(blockPointer);
            Free(skipPointer);
        }
    }

    /// <summary>
    /// 略過實際繪製並推進 libmpv render API 影格狀態。
    /// </summary>
    /// <param name="blockForTargetTime">是否等待影格目標時間。</param>
    public void SkipRender(bool blockForTargetTime = true)
    {
        EnsureNotDisposed();

        IntPtr blockPointer = IntPtr.Zero;
        IntPtr skipPointer = IntPtr.Zero;
        try
        {
            blockPointer = AllocInt32(blockForTargetTime ? 1 : 0);
            skipPointer = AllocInt32(1);
            MpvRenderParam[] parameters = new[]
            {
                new MpvRenderParam(MpvRenderParamType.BlockForTargetTime, blockPointer),
                new MpvRenderParam(MpvRenderParamType.SkipRendering, skipPointer),
                MpvRenderParam.Terminator
            };

            MpvError.ThrowIfError(MpvNative.mpv_render_context_render(_context, parameters));
        }
        finally
        {
            Free(blockPointer);
            Free(skipPointer);
        }
    }

    /// <summary>
    /// 取得 libmpv 下一個 render API 影格的資訊。
    /// </summary>
    /// <returns>下一個影格的 render API 資訊。</returns>
    public MpvRenderFrameInformation GetNextFrameInformation()
    {
        EnsureNotDisposed();

        IntPtr infoPointer = IntPtr.Zero;
        try
        {
            infoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<MpvRenderFrameInfo>());
            Marshal.StructureToPtr(new MpvRenderFrameInfo(), infoPointer, false);
            MpvRenderParam parameter = new MpvRenderParam(MpvRenderParamType.NextFrameInfo, infoPointer);
            MpvError.ThrowIfError(MpvNative.mpv_render_context_get_info(_context, parameter));
            MpvRenderFrameInfo info = Marshal.PtrToStructure<MpvRenderFrameInfo>(infoPointer);
            return new MpvRenderFrameInformation((MpvRenderFrameInfoFlags)info.Flags, info.TargetTime);
        }
        finally
        {
            Free(infoPointer);
        }
    }

    /// <summary>
    /// 設定 libmpv render API 內容參數。
    /// </summary>
    /// <param name="type">要設定的 render API 參數型別。</param>
    /// <param name="data">指向參數資料的原生指標。</param>
    public void SetParameter(MpvRenderParamType type, IntPtr data)
    {
        EnsureNotDisposed();
        MpvError.ThrowIfError(MpvNative.mpv_render_context_set_parameter(_context, new MpvRenderParam(type, data)));
    }

    /// <summary>
    /// 取得 libmpv render API 內容資訊。
    /// </summary>
    /// <param name="type">要查詢的 render API 資訊型別。</param>
    /// <param name="data">指向接收資訊資料的原生指標。</param>
    public void GetInformation(MpvRenderParamType type, IntPtr data)
    {
        EnsureNotDisposed();
        MpvError.ThrowIfError(MpvNative.mpv_render_context_get_info(_context, new MpvRenderParam(type, data)));
    }

    /// <summary>
    /// 設定 ICC 色彩描述檔資料。
    /// </summary>
    /// <param name="profile">ICC 色彩描述檔位元組資料。</param>
    public void SetIccProfile(byte[] profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        EnsureNotDisposed();

        IntPtr profilePointer = IntPtr.Zero;
        IntPtr byteArrayPointer = IntPtr.Zero;
        try
        {
            profilePointer = Marshal.AllocHGlobal(profile.Length);
            if (profile.Length > 0)
            {
                Marshal.Copy(profile, 0, profilePointer, profile.Length);
            }

            byteArrayPointer = AllocStructure(new NativeMpvByteArray
            {
                Data = profilePointer,
                Size = new UIntPtr(unchecked((ulong)profile.Length))
            });
            SetParameter(MpvRenderParamType.IccProfile, byteArrayPointer);
        }
        finally
        {
            Free(profilePointer);
            Free(byteArrayPointer);
        }
    }

    /// <summary>
    /// 清除目前設定的 ICC 色彩描述檔資料。
    /// </summary>
    public void ClearIccProfile()
    {
        SetIccProfile(Array.Empty<byte>());
    }

    /// <summary>
    /// 設定環境光照度。
    /// </summary>
    /// <param name="lux">以 lux 表示的環境光照度。</param>
    /// <remarks>
    /// libmpv 0.40（client API 版本 2.5）已將對應的
    /// <c>MPV_RENDER_PARAM_AMBIENT_LIGHT</c> 標記為 deprecated 且無替代品。
    /// </remarks>
    [Obsolete("libmpv 0.40 已 deprecate MPV_RENDER_PARAM_AMBIENT_LIGHT，並無替代參數；本方法已對應為 obsolete。")]
    public void SetAmbientLight(int lux)
    {
        EnsureNotDisposed();
        IntPtr valuePointer = IntPtr.Zero;
        try
        {
            valuePointer = AllocInt32(lux);
#pragma warning disable CS0618 // 仍須把值傳給 libmpv 已 deprecated 的參數，保留以維持 ABI。
            SetParameter(MpvRenderParamType.AmbientLight, valuePointer);
#pragma warning restore CS0618
        }
        finally
        {
            Free(valuePointer);
        }
    }

    /// <summary>
    /// 通知 libmpv 呼叫端已交換顯示緩衝區。
    /// </summary>
    public void ReportSwap()
    {
        EnsureNotDisposed();
        MpvNative.mpv_render_context_report_swap(_context);
    }

    /// <summary>
    /// 釋放 libmpv render API 內容。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_context != IntPtr.Zero)
        {
            MpvNative.mpv_render_context_set_update_callback(_context, null, IntPtr.Zero);
            MpvNative.mpv_render_context_free(_context);
            _context = IntPtr.Zero;
        }

        GC.KeepAlive(_updateCallback);
    }

    /// <summary>
    /// 處理 libmpv render API 更新通知並引發受控事件。
    /// </summary>
    /// <param name="context">libmpv 傳回的回呼內容指標。</param>
    private void OnRenderUpdate(IntPtr context)
    {
        UpdateAvailable?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 確認目前 render API 內容尚未釋放。
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    /// <summary>
    /// 依軟體 render 像素格式取得最小步幅。
    /// </summary>
    /// <param name="width">目標表面寬度。</param>
    /// <param name="format">mpv software render 像素格式。</param>
    /// <returns>可容納一列像素的最小位元組數。</returns>
    private static int GetMinimumStride(int width, string format)
    {
        if (string.Equals(format, "rgb24", StringComparison.OrdinalIgnoreCase))
        {
            return checked(width * 3);
        }

        return checked(width * 4);
    }

    /// <summary>
    /// 配置原生記憶體並寫入指定結構。
    /// </summary>
    /// <typeparam name="T">要寫入原生記憶體的結構型別。</typeparam>
    /// <param name="value">要寫入的結構值。</param>
    /// <returns>包含指定結構的原生記憶體指標。</returns>
    private static IntPtr AllocStructure<T>(T value) where T : struct
    {
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(value, pointer, false);
        return pointer;
    }

    /// <summary>
    /// 配置原生記憶體並寫入 32 位元整數。
    /// </summary>
    /// <param name="value">要寫入原生記憶體的整數值。</param>
    /// <returns>包含指定整數的原生記憶體指標。</returns>
    private static IntPtr AllocInt32(int value)
    {
        IntPtr pointer = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(pointer, value);
        return pointer;
    }

    /// <summary>
    /// 配置原生記憶體並寫入 size_t 值。
    /// </summary>
    /// <param name="value">要寫入的 size_t 值。</param>
    /// <returns>包含指定值的原生記憶體指標。</returns>
    private static IntPtr AllocSizeT(int value)
    {
        IntPtr pointer = Marshal.AllocHGlobal(IntPtr.Size);
        if (IntPtr.Size == 8)
        {
            Marshal.WriteInt64(pointer, value);
        }
        else
        {
            Marshal.WriteInt32(pointer, value);
        }

        return pointer;
    }

    /// <summary>
    /// 釋放先前配置的原生記憶體。
    /// </summary>
    /// <param name="pointer">要釋放的原生記憶體指標。</param>
    private static void Free(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
