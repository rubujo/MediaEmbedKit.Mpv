using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using MediaEmbedKit.Mpv.Native;
using MediaEmbedKit.Mpv.Render;
using MediaEmbedKit.Mpv.Runtime;

namespace MediaEmbedKit.Mpv.IntegrationTests;

/// <summary>
/// 以實際 Direct3D 11 裝置、紋理與 CPU 讀回驗證 libmpv 繪製。
/// </summary>
internal static unsafe class D3D11RenderProbe
{
    /// <summary>
    /// 驗證硬體裝置與 WARP 裝置，將影像與結果留在指定執行階段資料夾內。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 C API 2.6 libmpv 的資料夾。
    /// </param>
    /// <param name="full">
    /// 是否包含視窗呈現及硬體解碼驗證。
    /// </param>
    internal static void Run(string runtimeDirectory, bool full)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Direct3D 11 驗證需要 Windows。");
        }

        foreach (uint driver in new uint[] { 1, 5 })
        {
            RunDevice(runtimeDirectory, driver, full);
        }
    }

    /// <summary>
    /// 在單一裝置連續載入兩張影像，確認繪製結果確實隨媒體改變。
    /// </summary>
    /// <param name="runtimeDirectory">
    /// 包含 libmpv 的執行階段資料夾。
    /// </param>
    /// <param name="driver">
    /// Direct3D 裝置驅動程式型別。
    /// </param>
    /// <param name="full">
    /// 是否驗證視窗呈現及硬體解碼。
    /// </param>
    private static void RunDevice(string runtimeDirectory, uint driver, bool full)
    {
        IntPtr device = IntPtr.Zero;
        IntPtr immediate = IntPtr.Zero;
        IntPtr texture = IntPtr.Zero;
        IntPtr staging = IntPtr.Zero;
        IntPtr render = IntPtr.Zero;
        string label = driver == 1 ? "hardware" : "warp";
        string output = Path.Combine(runtimeDirectory, "d3d11-probe", label);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":false}");
        try
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(IntPtr.Zero, driver, IntPtr.Zero, 0x20,
                IntPtr.Zero, 0, 7, out device, out uint featureLevel, out immediate));
            var createTexture = (delegate* unmanaged[Stdcall]<IntPtr, TextureDescription*, IntPtr, IntPtr*, int>)Slot(device, 5);
            var description = new TextureDescription
            {
                Width = 64,
                Height = 64,
                MipLevels = 1,
                ArraySize = 1,
                Format = 87,
                SampleCount = 1,
                BindFlags = 0x20
            };
            IntPtr result;
            Marshal.ThrowExceptionForHR(createTexture(device, &description, IntPtr.Zero, &result));
            texture = result;
            description.Usage = 3;
            description.BindFlags = 0;
            description.CpuAccessFlags = 0x20000;
            Marshal.ThrowExceptionForHR(createTexture(device, &description, IntPtr.Zero, &result));
            staging = result;
            using D3D11PresentationWindow? window = full ? new D3D11PresentationWindow(device, immediate) : null;

            MpvPlayerOptions options = MpvRuntimeInstaller.CreatePlayerOptions(runtimeDirectory);
            options.EnableYtdlp = false;
            options.EnableOsc = false;
            options.InitialOptions["vo"] = "libmpv";
            options.InitialOptions["ao"] = "null";
            options.InitialOptions["hwdec"] = full && driver == 1 ? "d3d11va" : "no";
            options.InitialOptions["pause"] = "yes";
            options.InitialOptions["image-display-duration"] = "inf";
            using var player = new MpvPlayer(options);
            var diagnostics = new ConcurrentQueue<string>();
            player.LogMessageReceived += (_, message) =>
            {
                if (message.LogLevel >= MpvLogLevel.Fatal && message.LogLevel <= MpvLogLevel.Warn)
                {
                    diagnostics.Enqueue(message.Prefix + ": " + message.Text.Trim());
                }
            };
            player.Initialize();
            uint api = MpvPlayer.ClientApiVersion();
            IntegrationAssert.True(api >= ((2U << 16) | 6), "實際繪製驗證需要 C API 2.6。");
            var init = new MpvD3D11InitParams { Device = device };
            using var apiType = new Utf8String("d3d11");
            render = player.CreateRenderContext(new[]
            {
                new MpvRenderParam(MpvRenderParamType.ApiType, apiType.Pointer),
                new MpvRenderParam(MpvRenderParamType.D3D11InitParams, (IntPtr)(&init)),
                MpvRenderParam.Terminator
            });
            try
            {
                VerifyColor(player, render, immediate, texture, staging, output, true, window);
                VerifyColor(player, render, immediate, texture, staging, output, false, window);
                int decodedFrames = full && driver == 1
                    ? VerifyHardwareDecode(player, render, immediate, texture, staging, window!) : 0;
                string version = player.GetPropertyString("mpv-version") ?? throw new InvalidOperationException("無法讀取 mpv 版本。");
                player.Command("stop");
                Thread.Sleep(100);
                IntegrationAssert.True(diagnostics.IsEmpty, "繪製期間不得出現 libmpv 警告或錯誤：" + string.Join("; ", diagnostics));
                File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
                {
                    passed = true,
                    driver = label,
                    featureLevel,
                    clientApiVersion = api,
                    mpvVersion = version,
                    redAndBlueReadback = true,
                    swapChainPresentation = window != null,
                    windowPixelCheck = window != null,
                    hardwareDecode = full && driver == 1 ? "d3d11va" : "not-requested",
                    decodedFrames,
                    warningAndErrorCount = diagnostics.Count,
                    librarySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(runtimeDirectory, "libmpv-2.dll"))))
                }, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"[PASS] Direct3D 11 {label}：{version}，紅、藍影像紋理讀回通過，視窗呈現={window != null}，硬體解碼影格={decodedFrames}。");
            }
            finally
            {
                MpvNative.mpv_render_context_free(render);
                render = IntPtr.Zero;
            }
        }
        finally
        {
            Release(staging);
            Release(texture);
            Release(immediate);
            Release(device);
        }
    }

    /// <summary>
    /// 逐影格繪製並讀回中心區域；逾時或像素不符即失敗。
    /// </summary>
    /// <param name="player">
    /// 測試使用的播放器。
    /// </param>
    /// <param name="render">
    /// libmpv 繪製內容指標。
    /// </param>
    /// <param name="immediate">
    /// 同一裝置的即時內容。
    /// </param>
    /// <param name="texture">
    /// libmpv 繪入的紋理。
    /// </param>
    /// <param name="staging">
    /// CPU 可讀取的暫存紋理。
    /// </param>
    /// <param name="output">
    /// 驗證產物的資料夾。
    /// </param>
    /// <param name="red">
    /// 是否預期紅色影像。
    /// </param>
    /// <param name="window">
    /// 測試視窗控制代碼或呈現物件。
    /// </param>
    private static void VerifyColor(MpvPlayer player, IntPtr render, IntPtr immediate,
        IntPtr texture, IntPtr staging, string output, bool red, D3D11PresentationWindow? window)
    {
        string name = red ? "red" : "blue";
        string source = Path.Combine(output, name + ".ppm");
        byte[] image = new byte[64 * 64 * 3];
        for (int i = 0; i < image.Length; i += 3)
        {
            image[i + (red ? 0 : 2)] = 255;
        }

        using (FileStream file = File.Create(source))
        {
            file.Write(Encoding.ASCII.GetBytes("P6\n64 64\n255\n"));
            file.Write(image);
        }

        player.Load(new MpvMediaItem(source));
        var fbo = new MpvD3D11Fbo { Texture = texture };
        int block = 0;
        var copy = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>)Slot(immediate, 47);
        var map = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, uint, MappedResource*, int>)Slot(immediate, 14);
        var unmap = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)Slot(immediate, 15);
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(15))
        {
            if ((MpvNative.mpv_render_context_update(render) & 1) == 0)
            {
                Thread.Sleep(10);
                continue;
            }

            MpvError.ThrowIfError(MpvNative.mpv_render_context_render(render, new[]
            {
                new MpvRenderParam(MpvRenderParamType.D3D11Fbo, (IntPtr)(&fbo)),
                new MpvRenderParam(MpvRenderParamType.BlockForTargetTime, (IntPtr)(&block)),
                MpvRenderParam.Terminator
            }));
            MpvNative.mpv_render_context_report_swap(render);
            copy(immediate, staging, texture);
            MappedResource mapped;
            Marshal.ThrowExceptionForHR(map(immediate, staging, 0, 1, 0, &mapped));
            bool matches = true;
            try
            {
                for (int y = 16; y < 48; y++)
                {
                    for (int x = 16; x < 48; x++)
                    {
                        byte* pixel = (byte*)mapped.Data + y * mapped.RowPitch + x * 4;
                        matches &= pixel[red ? 2 : 0] > 200 && pixel[1] < 40 && pixel[red ? 0 : 2] < 40;
                    }
                }

                if (matches)
                {
                    using FileStream file = File.Create(Path.Combine(output, name + "-readback.ppm"));
                    file.Write(Encoding.ASCII.GetBytes("P6\n64 64\n255\n"));
                    for (int y = 0; y < 64; y++)
                    {
                        for (int x = 0; x < 64; x++)
                        {
                            byte* pixel = (byte*)mapped.Data + y * mapped.RowPitch + x * 4;
                            file.WriteByte(pixel[2]);
                            file.WriteByte(pixel[1]);
                            file.WriteByte(pixel[0]);
                        }
                    }
                }
            }
            finally
            {
                unmap(immediate, staging, 0);
            }

            if (matches)
            {
                window?.VerifyColor(texture, red);
                return;
            }
        }

        throw new TimeoutException("Direct3D 11 紋理讀回未取得預期的" + name + "影像。");
    }

    /// <summary>
    /// 確認 H.264 使用 d3d11va 解碼，持續繪製及呈現紅、藍兩段影像。
    /// </summary>
    /// <param name="player">
    /// 測試使用的播放器。
    /// </param>
    /// <param name="render">
    /// libmpv 繪製內容指標。
    /// </param>
    /// <param name="immediate">
    /// 同一裝置的即時內容。
    /// </param>
    /// <param name="texture">
    /// libmpv 繪入的紋理。
    /// </param>
    /// <param name="staging">
    /// CPU 可讀取的暫存紋理。
    /// </param>
    /// <param name="window">
    /// 測試視窗控制代碼或呈現物件。
    /// </param>
    /// <returns>
    /// 已完成硬體解碼及呈現的影格數。
    /// </returns>
    private static int VerifyHardwareDecode(MpvPlayer player, IntPtr render, IntPtr immediate,
        IntPtr texture, IntPtr staging, D3D11PresentationWindow window)
    {
        string source = Environment.GetEnvironmentVariable("MEDIAEMBEDKIT_MPV_D3D11_VIDEO")
            ?? throw new InvalidOperationException("必須以 MEDIAEMBEDKIT_MPV_D3D11_VIDEO 指定紅、藍兩段 H.264 測試影片。");
        IntegrationAssert.True(File.Exists(source), "硬體解碼測試影片必須存在。");
        player.Load(new MpvMediaItem(source));
        player.SetPropertyFlag("pause", false);
        var fbo = new MpvD3D11Fbo { Texture = texture };
        int block = 0;
        var copy = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>)Slot(immediate, 47);
        var map = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, uint, MappedResource*, int>)Slot(immediate, 14);
        var unmap = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)Slot(immediate, 15);
        int frames = 0;
        bool sawRed = false;
        bool sawBlue = false;
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(20))
        {
            if ((MpvNative.mpv_render_context_update(render) & 1) == 0)
            {
                Thread.Sleep(5);
                continue;
            }

            MpvError.ThrowIfError(MpvNative.mpv_render_context_render(render, new[]
            {
                new MpvRenderParam(MpvRenderParamType.D3D11Fbo, (IntPtr)(&fbo)),
                new MpvRenderParam(MpvRenderParamType.BlockForTargetTime, (IntPtr)(&block)),
                MpvRenderParam.Terminator
            }));
            window.Present(texture);
            MpvNative.mpv_render_context_report_swap(render);
            if (!player.TryGetPropertyString("hwdec-current", out string? hwdec) || hwdec != "d3d11va")
            {
                continue;
            }

            frames++;
            copy(immediate, staging, texture);
            MappedResource mapped;
            Marshal.ThrowExceptionForHR(map(immediate, staging, 0, 1, 0, &mapped));
            bool red;
            bool blue;
            try
            {
                byte* pixel = (byte*)mapped.Data + 32 * mapped.RowPitch + 32 * 4;
                red = pixel[2] > 200 && pixel[1] < 40 && pixel[0] < 40;
                blue = pixel[0] > 200 && pixel[1] < 40 && pixel[2] < 40;
            }
            finally
            {
                unmap(immediate, staging, 0);
            }

            if (red && !sawRed)
            {
                window.VerifyColor(texture, true);
                sawRed = true;
            }
            if (blue && sawRed && !sawBlue)
            {
                window.VerifyColor(texture, false);
                sawBlue = true;
            }
            if (frames >= 30 && sawRed && sawBlue)
            {
                IntegrationAssert.True(player.GetPropertyDouble("time-pos") >= 2.5, "播放時間必須跨越紅、藍片段。");
                return frames;
            }
        }

        throw new InvalidOperationException("未能以 d3d11va 完成紅、藍影片的連續繪製與視窗呈現。");
    }

    /// <summary>
    /// 取得 Windows SDK d3d11.h 宣告順序中的 COM 函式指標。
    /// </summary>
    /// <param name="instance">
    /// 原生物件指標。
    /// </param>
    /// <param name="index">
    /// COM 虛擬函式索引。
    /// </param>
    /// <returns>
    /// COM 函式指標。
    /// </returns>
    private static void* Slot(IntPtr instance, int index) => ((void**)*(void**)instance)[index];

    /// <summary>
    /// 釋放測試持有的 COM 參考。
    /// </summary>
    /// <param name="instance">
    /// 原生物件指標。
    /// </param>
    private static void Release(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
        {
            _ = ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(instance, 2))(instance);
        }
    }

    /// <summary>
    /// 建立 Direct3D 11 裝置及即時內容。
    /// </summary>
    /// <param name="adapter">
    /// 顯示介面卡指標。
    /// </param>
    /// <param name="driverType">
    /// Direct3D 驅動程式型別。
    /// </param>
    /// <param name="software">
    /// 軟體光柵化模組指標。
    /// </param>
    /// <param name="flags">
    /// 原生 API 旗標。
    /// </param>
    /// <param name="featureLevels">
    /// 要求的功能層級陣列。
    /// </param>
    /// <param name="featureLevelCount">
    /// 功能層級陣列長度。
    /// </param>
    /// <param name="sdkVersion">
    /// Direct3D SDK 版本。
    /// </param>
    /// <param name="device">
    /// 建立的 Direct3D 裝置。
    /// </param>
    /// <param name="featureLevel">
    /// 實際功能層級。
    /// </param>
    /// <param name="immediateContext">
    /// 建立的裝置即時內容。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(IntPtr adapter, uint driverType, IntPtr software,
        uint flags, IntPtr featureLevels, uint featureLevelCount, uint sdkVersion,
        out IntPtr device, out uint featureLevel, out IntPtr immediateContext);

    /// <summary>
    /// 對應 D3D11_TEXTURE2D_DESC。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TextureDescription
    {
        /// <summary>
        /// 紋理寬度。
        /// </summary>
        public uint Width;
        /// <summary>
        /// 紋理高度。
        /// </summary>
        public uint Height;
        /// <summary>
        /// 紋理階層數。
        /// </summary>
        public uint MipLevels;
        /// <summary>
        /// 紋理陣列長度。
        /// </summary>
        public uint ArraySize;
        /// <summary>
        /// DXGI 像素格式。
        /// </summary>
        public uint Format;
        /// <summary>
        /// 每像素取樣數。
        /// </summary>
        public uint SampleCount;
        /// <summary>
        /// 取樣品質層級。
        /// </summary>
        public uint SampleQuality;
        /// <summary>
        /// 資源使用方式。
        /// </summary>
        public uint Usage;
        /// <summary>
        /// 資源繫結旗標。
        /// </summary>
        public uint BindFlags;
        /// <summary>
        /// CPU 存取旗標。
        /// </summary>
        public uint CpuAccessFlags;
        /// <summary>
        /// 資源附加旗標。
        /// </summary>
        public uint MiscFlags;
    }

    /// <summary>
    /// 對應 D3D11_MAPPED_SUBRESOURCE。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MappedResource
    {
        /// <summary>
        /// 對應資料的指標。
        /// </summary>
        public IntPtr Data;
        /// <summary>
        /// 每列資料位元組數。
        /// </summary>
        public uint RowPitch;
        /// <summary>
        /// 每層資料位元組數。
        /// </summary>
        public uint DepthPitch;
    }
}
