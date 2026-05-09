using System;
using System.Runtime.InteropServices;
using MediaEmbedKit.Mpv.Render;

namespace MediaEmbedKit.Mpv.Native
{
    /// <summary>
    /// 表示 libmpv OpenGL 函式位址解析回呼。
    /// </summary>
    /// <param name="context">呼叫端提供的回呼內容指標。</param>
    /// <param name="name">要解析的 OpenGL 函式名稱指標。</param>
    /// <returns>OpenGL 函式位址；無法解析時為 <see cref="IntPtr.Zero"/>。</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate IntPtr MpvOpenGlGetProcAddress(IntPtr context, IntPtr name);

    /// <summary>
    /// 表示 libmpv render API 更新通知回呼。
    /// </summary>
    /// <param name="context">呼叫端提供的回呼內容指標。</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MpvRenderUpdateCallback(IntPtr context);

    /// <summary>
    /// 對應 libmpv render API 參數結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvRenderParam
    {
        /// <summary>
        /// 初始化 <see cref="MpvRenderParam"/> 結構的新執行個體。
        /// </summary>
        /// <param name="type">render API 參數型別。</param>
        /// <param name="data">render API 參數資料指標。</param>
        public MpvRenderParam(MpvRenderParamType type, IntPtr data)
        {
            Type = type;
            Data = data;
        }

        /// <summary>
        /// 表示 render API 參數陣列結尾的終止參數。
        /// </summary>
        public static readonly MpvRenderParam Terminator = new MpvRenderParam(MpvRenderParamType.Invalid, IntPtr.Zero);

        /// <summary>
        /// render API 參數型別。
        /// </summary>
        public MpvRenderParamType Type;

        /// <summary>
        /// render API 參數資料指標。
        /// </summary>
        public IntPtr Data;
    }

    /// <summary>
    /// 對應 libmpv OpenGL 初始化參數結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvOpenGlInitParams
    {
        /// <summary>
        /// OpenGL 函式位址解析回呼。
        /// </summary>
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public MpvOpenGlGetProcAddress GetProcAddress;

        /// <summary>
        /// OpenGL 函式位址解析回呼內容指標。
        /// </summary>
        public IntPtr GetProcAddressContext;
    }

    /// <summary>
    /// 對應 libmpv OpenGL framebuffer 目標結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvOpenGlFbo
    {
        /// <summary>
        /// 初始化 <see cref="MpvOpenGlFbo"/> 結構的新執行個體。
        /// </summary>
        /// <param name="fbo">OpenGL framebuffer 物件識別碼。</param>
        /// <param name="width">framebuffer 寬度。</param>
        /// <param name="height">framebuffer 高度。</param>
        /// <param name="internalFormat">framebuffer 內部格式。</param>
        public MpvOpenGlFbo(int fbo, int width, int height, int internalFormat)
        {
            Fbo = fbo;
            Width = width;
            Height = height;
            InternalFormat = internalFormat;
        }

        /// <summary>
        /// OpenGL framebuffer 物件識別碼。
        /// </summary>
        public int Fbo;

        /// <summary>
        /// framebuffer 寬度。
        /// </summary>
        public int Width;

        /// <summary>
        /// framebuffer 高度。
        /// </summary>
        public int Height;

        /// <summary>
        /// framebuffer 內部格式。
        /// </summary>
        public int InternalFormat;
    }

    /// <summary>
    /// 對應 libmpv 舊版 OpenGL DRM 顯示參數結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvOpenGlDrmParams
    {
        /// <summary>
        /// DRM 檔案描述元。
        /// </summary>
        public int FileDescriptor;

        /// <summary>
        /// 目前使用的 CRTC 識別碼。
        /// </summary>
        public int CrtcId;

        /// <summary>
        /// 目前使用的連接器識別碼。
        /// </summary>
        public int ConnectorId;

        /// <summary>
        /// 指向 DRM atomic request 指標的指標。
        /// </summary>
        public IntPtr AtomicRequestPointer;

        /// <summary>
        /// DRM render node 檔案描述元。
        /// </summary>
        public int RenderFileDescriptor;
    }

    /// <summary>
    /// 對應 libmpv OpenGL DRM 繪圖表面大小結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvOpenGlDrmDrawSurfaceSize
    {
        /// <summary>
        /// DRM 繪圖表面寬度。
        /// </summary>
        public int Width;

        /// <summary>
        /// DRM 繪圖表面高度。
        /// </summary>
        public int Height;
    }

    /// <summary>
    /// 對應 libmpv 第二版 OpenGL DRM 顯示參數結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvOpenGlDrmParamsV2
    {
        /// <summary>
        /// DRM 檔案描述元。
        /// </summary>
        public int FileDescriptor;

        /// <summary>
        /// 目前使用的 CRTC 識別碼。
        /// </summary>
        public int CrtcId;

        /// <summary>
        /// 目前使用的連接器識別碼。
        /// </summary>
        public int ConnectorId;

        /// <summary>
        /// 指向 DRM atomic request 指標的指標。
        /// </summary>
        public IntPtr AtomicRequestPointer;

        /// <summary>
        /// DRM render node 檔案描述元。
        /// </summary>
        public int RenderFileDescriptor;
    }

    /// <summary>
    /// 對應 libmpv 下一個 render API 影格資訊結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvRenderFrameInfo
    {
        /// <summary>
        /// 下一個影格資訊旗標。
        /// </summary>
        public ulong Flags;

        /// <summary>
        /// 下一個影格目標顯示時間。
        /// </summary>
        public long TargetTime;
    }
}
