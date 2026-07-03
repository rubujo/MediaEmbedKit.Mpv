using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MediaEmbedKit.Mpv.Native;

namespace MediaEmbedKit.Mpv.Render;

/// <summary>
/// 封裝 libmpv OpenGL render API 內容。
/// </summary>
/// <remarks>
/// <para>
/// OpenGL render context 必須由呼叫端在具備活動 OpenGL Context 的擁有執行緒上
/// 決定性呼叫 <see cref="Dispose"/> 釋放。GC Finalizer 執行緒通常不具備活動 OpenGL
/// Context，因此 Finalizer 僅做最小保護，不會嘗試呼叫原生釋放 API。
/// </para>
/// </remarks>
public sealed class MpvOpenGlRenderContext : IDisposable
{
    /// <summary>
    /// 序列化所有 libmpv render API 呼叫與釋放流程的同步物件。
    /// </summary>
    private readonly object _syncRoot = new object();
    /// <summary>
    /// 保存解析 OpenGL 函式位址的委派，避免遭到記憶體回收。
    /// </summary>
    private readonly MpvOpenGlGetProcAddress _getProcAddressCallback;
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
    /// 通知擁有端此 render API 內容已釋放的回呼。
    /// </summary>
    private readonly Action<IDisposable>? _disposeCallback;

    /// <summary>
    /// 初始化 <see cref="MpvOpenGlRenderContext"/> 類別的新執行個體。
    /// </summary>
    /// <param name="context">
    /// libmpv render API 內容指標。
    /// </param>
    /// <param name="getProcAddressCallback">
    /// OpenGL 函式位址解析委派。
    /// </param>
    /// <param name="disposeCallback">
    /// 釋放時通知擁有端的回呼。
    /// </param>
    private MpvOpenGlRenderContext(IntPtr context, MpvOpenGlGetProcAddress getProcAddressCallback, Action<IDisposable>? disposeCallback)
    {
        _context = context;
        _getProcAddressCallback = getProcAddressCallback;
        _disposeCallback = disposeCallback;
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
    /// <value>
    /// 已釋放時為 <see langword="true"/>。
    /// </value>
    public bool IsDisposed
    {
        get
        {
            lock (_syncRoot)
            {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// 取得 libmpv render API 內容的原生控制代碼。
    /// </summary>
    /// <value>
    /// libmpv render API 內容指標。
    /// </value>
    public IntPtr DangerousHandle
    {
        get
        {
            lock (_syncRoot)
            {
                EnsureNotDisposed();
                return _context;
            }
        }
    }

    /// <summary>
    /// 為指定播放器建立 OpenGL render API 內容。
    /// </summary>
    /// <param name="player">
    /// 要關聯的 mpv 播放器。
    /// </param>
    /// <param name="options">
    /// OpenGL render API 建立選項。
    /// </param>
    /// <returns>
    /// 新建立的 OpenGL render API 內容。
    /// </returns>
    public static MpvOpenGlRenderContext Create(MpvPlayer player, MpvOpenGlRenderContextOptions options)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        return player.CreateOpenGlRenderContext(options);
    }

    /// <summary>
    /// 建立不自行登錄擁有關係的 OpenGL render API 內容；呼叫端必須負責追蹤其生命週期。
    /// </summary>
    /// <param name="player">
    /// 要關聯的 mpv 播放器。
    /// </param>
    /// <param name="options">
    /// OpenGL render API 建立選項。
    /// </param>
    /// <param name="disposeCallback">
    /// 釋放時通知擁有端的回呼。
    /// </param>
    /// <returns>
    /// 新建立的 OpenGL render API 內容。
    /// </returns>
    internal static MpvOpenGlRenderContext CreateUntracked(MpvPlayer player, MpvOpenGlRenderContextOptions options, Action<IDisposable>? disposeCallback)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        MpvOpenGlGetProcAddress callback = (ctx, name) =>
        {
            string? symbolName = Utf8StringMarshaller.PtrToString(name);
            return string.IsNullOrEmpty(symbolName) ? IntPtr.Zero : options.GetProcAddress(symbolName!);
        };

        using (Utf8String apiType = new Utf8String("opengl"))
        {
            IntPtr initParamsPointer = IntPtr.Zero;
            IntPtr advancedControlPointer = IntPtr.Zero;
            IntPtr drmDisplayPointer = IntPtr.Zero;
            IntPtr drmDrawSurfaceSizePointer = IntPtr.Zero;
            IntPtr drmDisplayV2Pointer = IntPtr.Zero;
            try
            {
                MpvOpenGlInitParams initParams = new MpvOpenGlInitParams
                {
                    GetProcAddress = callback,
                    GetProcAddressContext = options.GetProcAddressContext
                };

                initParamsPointer = AllocStructure(initParams);

                List<MpvRenderParam> parameters = new List<MpvRenderParam>
                {
                    new MpvRenderParam(MpvRenderParamType.ApiType, apiType.Pointer),
                    new MpvRenderParam(MpvRenderParamType.OpenGlInitParams, initParamsPointer)
                };

                if (options.AdvancedControl)
                {
                    advancedControlPointer = AllocInt32(1);
                    parameters.Add(new MpvRenderParam(MpvRenderParamType.AdvancedControl, advancedControlPointer));
                }

                parameters.Add(new MpvRenderParam(MpvRenderParamType.X11Display, options.X11Display));
                parameters.Add(new MpvRenderParam(MpvRenderParamType.WaylandDisplay, options.WaylandDisplay));

                if (options.DrmDisplay != null)
                {
                    drmDisplayPointer = AllocStructure(ToNativeDrmParams(options.DrmDisplay));
                    parameters.Add(new MpvRenderParam(MpvRenderParamType.DrmDisplay, drmDisplayPointer));
                }

                if (options.DrmDrawSurfaceSize != null)
                {
                    drmDrawSurfaceSizePointer = AllocStructure(new MediaEmbedKit.Mpv.Native.MpvOpenGlDrmDrawSurfaceSize
                    {
                        Width = options.DrmDrawSurfaceSize.Width,
                        Height = options.DrmDrawSurfaceSize.Height
                    });
                    parameters.Add(new MpvRenderParam(MpvRenderParamType.DrmDrawSurfaceSize, drmDrawSurfaceSizePointer));
                }

                if (options.DrmDisplayV2 != null)
                {
                    drmDisplayV2Pointer = AllocStructure(ToNativeDrmParamsV2(options.DrmDisplayV2));
                    parameters.Add(new MpvRenderParam(MpvRenderParamType.DrmDisplayV2, drmDisplayV2Pointer));
                }

                parameters.Add(MpvRenderParam.Terminator);

                IntPtr context = player.CreateRenderContext(parameters.ToArray());
                return new MpvOpenGlRenderContext(context, callback, disposeCallback);
            }
            finally
            {
                Free(initParamsPointer);
                Free(advancedControlPointer);
                Free(drmDisplayPointer);
                Free(drmDrawSurfaceSizePointer);
                Free(drmDisplayV2Pointer);
            }
        }
    }

    /// <summary>
    /// 讀取並清除 libmpv render API 的更新旗標。
    /// </summary>
    /// <returns>
    /// 目前待處理的 render API 更新旗標。
    /// </returns>
    public MpvRenderUpdateFlags Update()
    {
        lock (_syncRoot)
        {
            EnsureNotDisposed();
            return (MpvRenderUpdateFlags)MpvNative.mpv_render_context_update(_context);
        }
    }

    /// <summary>
    /// 將 mpv 目前影格繪製到指定的 OpenGL framebuffer。
    /// </summary>
    /// <param name="framebufferObject">
    /// OpenGL framebuffer 物件識別碼。
    /// </param>
    /// <param name="width">
    /// framebuffer 寬度。
    /// </param>
    /// <param name="height">
    /// framebuffer 高度。
    /// </param>
    /// <param name="internalFormat">
    /// OpenGL framebuffer 內部格式。
    /// </param>
    /// <param name="flipY">
    /// 是否垂直翻轉輸出。
    /// </param>
    /// <param name="blockForTargetTime">
    /// 是否等待影格目標時間。
    /// </param>
    /// <param name="depth">
    /// framebuffer 深度值。
    /// </param>
    /// <param name="skipRendering">
    /// 是否略過實際繪製並只推進 render API 影格狀態。
    /// </param>
    public void Render(
        int framebufferObject,
        int width,
        int height,
        int internalFormat = 0,
        bool flipY = true,
        bool blockForTargetTime = true,
        int depth = 0,
        bool skipRendering = false)
    {
        IntPtr fboPointer = IntPtr.Zero;
        IntPtr flipPointer = IntPtr.Zero;
        IntPtr blockPointer = IntPtr.Zero;
        IntPtr depthPointer = IntPtr.Zero;
        IntPtr skipPointer = IntPtr.Zero;
        lock (_syncRoot)
        {
            EnsureNotDisposed();
            try
            {
                fboPointer = AllocStructure(new MpvOpenGlFbo(framebufferObject, width, height, internalFormat));
                flipPointer = AllocInt32(flipY ? 1 : 0);
                blockPointer = AllocInt32(blockForTargetTime ? 1 : 0);
                depthPointer = AllocInt32(depth);
                skipPointer = AllocInt32(skipRendering ? 1 : 0);

                MpvRenderParam[] parameters = new[]
                {
                    new MpvRenderParam(MpvRenderParamType.OpenGlFbo, fboPointer),
                    new MpvRenderParam(MpvRenderParamType.FlipY, flipPointer),
                    new MpvRenderParam(MpvRenderParamType.BlockForTargetTime, blockPointer),
                    new MpvRenderParam(MpvRenderParamType.Depth, depthPointer),
                    new MpvRenderParam(MpvRenderParamType.SkipRendering, skipPointer),
                    MpvRenderParam.Terminator
                };

                MpvError.ThrowIfError(MpvNative.mpv_render_context_render(_context, parameters));
            }
            finally
            {
                Free(fboPointer);
                Free(flipPointer);
                Free(blockPointer);
                Free(depthPointer);
                Free(skipPointer);
            }
        }
    }

    /// <summary>
    /// 略過實際繪製並推進 libmpv render API 影格狀態。
    /// </summary>
    /// <param name="blockForTargetTime">
    /// 是否等待影格目標時間。
    /// </param>
    public void SkipRender(bool blockForTargetTime = true)
    {
        IntPtr blockPointer = IntPtr.Zero;
        IntPtr skipPointer = IntPtr.Zero;
        lock (_syncRoot)
        {
            EnsureNotDisposed();
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
    }

    /// <summary>
    /// 取得 libmpv 下一個 render API 影格的資訊。
    /// </summary>
    /// <returns>
    /// 下一個影格的 render API 資訊。
    /// </returns>
    public MpvRenderFrameInformation GetNextFrameInformation()
    {
        IntPtr infoPointer = IntPtr.Zero;
        lock (_syncRoot)
        {
            EnsureNotDisposed();
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
    }

    /// <summary>
    /// 設定 libmpv render API 內容參數。
    /// </summary>
    /// <param name="type">
    /// 要設定的 render API 參數型別。
    /// </param>
    /// <param name="data">
    /// 指向參數資料的原生指標。
    /// </param>
    public void SetParameter(MpvRenderParamType type, IntPtr data)
    {
        lock (_syncRoot)
        {
            EnsureNotDisposed();
            MpvError.ThrowIfError(MpvNative.mpv_render_context_set_parameter(_context, new MpvRenderParam(type, data)));
        }
    }

    /// <summary>
    /// 取得 libmpv render API 內容資訊。
    /// </summary>
    /// <param name="type">
    /// 要查詢的 render API 資訊型別。
    /// </param>
    /// <param name="data">
    /// 指向接收資訊資料的原生指標。
    /// </param>
    public void GetInformation(MpvRenderParamType type, IntPtr data)
    {
        lock (_syncRoot)
        {
            EnsureNotDisposed();
            MpvError.ThrowIfError(MpvNative.mpv_render_context_get_info(_context, new MpvRenderParam(type, data)));
        }
    }

    /// <summary>
    /// 設定 ICC 色彩描述檔資料。
    /// </summary>
    /// <param name="profile">
    /// ICC 色彩描述檔位元組資料。
    /// </param>
    public void SetIccProfile(byte[] profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        IntPtr profilePointer = IntPtr.Zero;
        IntPtr byteArrayPointer = IntPtr.Zero;
        lock (_syncRoot)
        {
            EnsureNotDisposed();
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
    /// <param name="lux">
    /// 以 lux 表示的環境光照度。
    /// </param>
    /// <remarks>
    /// libmpv 0.40（client API 版本 2.5）已將對應的
    /// <c>MPV_RENDER_PARAM_AMBIENT_LIGHT</c> 標記為 deprecated 且無替代品。
    /// </remarks>
    [Obsolete("libmpv 0.40 已 deprecate MPV_RENDER_PARAM_AMBIENT_LIGHT，並無替代參數；本方法已對應為 obsolete。")]
    public void SetAmbientLight(int lux)
    {
        IntPtr valuePointer = IntPtr.Zero;
        lock (_syncRoot)
        {
            EnsureNotDisposed();
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
    }

    /// <summary>
    /// 通知 libmpv 呼叫端已交換顯示緩衝區。
    /// </summary>
    public void ReportSwap()
    {
        lock (_syncRoot)
        {
            EnsureNotDisposed();
            MpvNative.mpv_render_context_report_swap(_context);
        }
    }

    /// <summary>
    /// 釋放 libmpv render API 內容。
    /// </summary>
    public void Dispose()
    {
        bool shouldNotify = false;
        lock (_syncRoot)
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

            shouldNotify = true;
        }

        if (shouldNotify)
        {
            _disposeCallback?.Invoke(this);
        }

        GC.KeepAlive(_getProcAddressCallback);
        GC.KeepAlive(_updateCallback);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 釋放 <see cref="MpvOpenGlRenderContext"/> 類別的資源。
    /// </summary>
    ~MpvOpenGlRenderContext()
    {
        // 避免在 Finalizer 執行緒直接釋放 OpenGL render context。
        // OpenGL backend 需要活動 GL Context，GC Finalizer 執行緒通常不具備此條件。
        IntPtr context = Interlocked.Exchange(ref _context, IntPtr.Zero);
        if (context != IntPtr.Zero)
        {
            _disposed = true;
            Trace.TraceWarning("MpvOpenGlRenderContext 未經 Dispose 即進入 Finalizer。請在擁有 OpenGL Context 的執行緒決定性釋放。");
#if DEBUG
            Debug.Fail("MpvOpenGlRenderContext 必須決定性呼叫 Dispose()，不可依賴 Finalizer 釋放。");
#endif
        }
    }

    /// <summary>
    /// 處理 libmpv render API 更新通知並引發受控事件。
    /// </summary>
    /// <param name="context">
    /// libmpv 傳回的回呼內容指標。
    /// </param>
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
    /// 將受控 DRM 顯示選項轉換為舊版原生 DRM 顯示參數。
    /// </summary>
    /// <param name="options">
    /// 受控 DRM 顯示選項。
    /// </param>
    /// <returns>
    /// 舊版原生 DRM 顯示參數。
    /// </returns>
    private static MpvOpenGlDrmParams ToNativeDrmParams(MpvOpenGlDrmDisplayOptions options)
    {
        return new MpvOpenGlDrmParams
        {
            FileDescriptor = options.FileDescriptor,
            CrtcId = options.CrtcId,
            ConnectorId = options.ConnectorId,
            AtomicRequestPointer = options.AtomicRequestPointer,
            RenderFileDescriptor = options.RenderFileDescriptor
        };
    }

    /// <summary>
    /// 將受控 DRM 顯示選項轉換為第二版原生 DRM 顯示參數。
    /// </summary>
    /// <param name="options">
    /// 受控 DRM 顯示選項。
    /// </param>
    /// <returns>
    /// 第二版原生 DRM 顯示參數。
    /// </returns>
    private static MpvOpenGlDrmParamsV2 ToNativeDrmParamsV2(MpvOpenGlDrmDisplayOptions options)
    {
        return new MpvOpenGlDrmParamsV2
        {
            FileDescriptor = options.FileDescriptor,
            CrtcId = options.CrtcId,
            ConnectorId = options.ConnectorId,
            AtomicRequestPointer = options.AtomicRequestPointer,
            RenderFileDescriptor = options.RenderFileDescriptor
        };
    }

    /// <summary>
    /// 配置原生記憶體並寫入指定結構。
    /// </summary>
    /// <typeparam name="T">
    /// 要寫入原生記憶體的結構型別。
    /// </typeparam>
    /// <param name="value">
    /// 要寫入的結構值。
    /// </param>
    /// <returns>
    /// 包含指定結構的原生記憶體指標。
    /// </returns>
    private static IntPtr AllocStructure<T>(T value) where T : struct
    {
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(value, pointer, false);
        return pointer;
    }

    /// <summary>
    /// 配置原生記憶體並寫入 32 位元整數。
    /// </summary>
    /// <param name="value">
    /// 要寫入原生記憶體的整數值。
    /// </param>
    /// <returns>
    /// 包含指定整數的原生記憶體指標。
    /// </returns>
    private static IntPtr AllocInt32(int value)
    {
        IntPtr pointer = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(pointer, value);
        return pointer;
    }

    /// <summary>
    /// 釋放先前配置的原生記憶體。
    /// </summary>
    /// <param name="pointer">
    /// 要釋放的原生記憶體指標。
    /// </param>
    private static void Free(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
