using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace MediaEmbedKit.Mpv.IntegrationTests;

/// <summary>
/// 以 DXGI 交換鏈將測試紋理呈現至原生視窗，並讀取實際視窗像素。
/// </summary>
internal sealed unsafe class D3D11PresentationWindow : IDisposable
{
    /// <summary>
    /// 測試視窗控制代碼。
    /// </summary>
    private IntPtr _window;
    /// <summary>
    /// DXGI 交換鏈。
    /// </summary>
    private IntPtr _swapChain;
    /// <summary>
    /// 交換鏈的繪製緩衝區。
    /// </summary>
    private IntPtr _buffer;
    /// <summary>
    /// 由呼叫端持有的裝置即時內容。
    /// </summary>
    private readonly IntPtr _immediate;

    /// <summary>
    /// 建立可見視窗與 64 × 64 BGRA 交換鏈。
    /// </summary>
    /// <param name="device">
    /// 測試使用的 ID3D11Device。
    /// </param>
    /// <param name="immediate">
    /// 同一裝置的 ID3D11DeviceContext。
    /// </param>
    internal D3D11PresentationWindow(IntPtr device, IntPtr immediate)
    {
        _immediate = immediate;
        IntPtr dxgiDevice = IntPtr.Zero;
        IntPtr adapter = IntPtr.Zero;
        IntPtr factory = IntPtr.Zero;
        try
        {
            _window = CreateWindowExW(0, "STATIC", "MediaEmbedKit.Mpv Direct3D 11 繪製驗證",
                0x10CF0000, 100, 100, 420, 420, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_window == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var deviceId = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, in deviceId, out dxgiDevice));
            IntPtr result;
            var getAdapter = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Slot(dxgiDevice, 7);
            Marshal.ThrowExceptionForHR(getAdapter(dxgiDevice, &result));
            adapter = result;
            var factoryId = new Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369");
            var getParent = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)Slot(adapter, 6);
            Marshal.ThrowExceptionForHR(getParent(adapter, &factoryId, &result));
            factory = result;
            var description = new SwapChainDescription
            {
                Width = 64,
                Height = 64,
                Format = 87,
                SampleCount = 1,
                BufferUsage = 0x20,
                BufferCount = 2,
                OutputWindow = _window,
                Windowed = 1
            };
            var create = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, SwapChainDescription*, IntPtr*, int>)Slot(factory, 10);
            Marshal.ThrowExceptionForHR(create(factory, device, &description, &result));
            _swapChain = result;
            var textureId = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
            var getBuffer = (delegate* unmanaged[Stdcall]<IntPtr, uint, Guid*, IntPtr*, int>)Slot(_swapChain, 9);
            Marshal.ThrowExceptionForHR(getBuffer(_swapChain, 0, &textureId, &result));
            _buffer = result;
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            Release(factory);
            Release(adapter);
            Release(dxgiDevice);
        }
    }

    /// <summary>
    /// 複製 libmpv 輸出並確認呈現成功。
    /// </summary>
    /// <param name="texture">
    /// libmpv 繪入的紋理。
    /// </param>
    internal void Present(IntPtr texture)
    {
        Pump();
        var copy = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void>)Slot(_immediate, 47);
        copy(_immediate, _buffer, texture);
        var present = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)Slot(_swapChain, 8);
        int result = present(_swapChain, 1, 0);
        IntegrationAssert.Equal(0, result, "DXGI Present 不可失敗或回報視窗被遮蔽。");
    }

    /// <summary>
    /// 確認桌面實際視窗像素符合預期，排除只有交換鏈呼叫成功的情況。
    /// </summary>
    /// <param name="texture">
    /// libmpv 繪入的純色紋理。
    /// </param>
    /// <param name="red">
    /// 是否預期紅色。
    /// </param>
    internal void VerifyColor(IntPtr texture, bool red)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Present(texture);
            Marshal.ThrowExceptionForHR(DwmFlush());
            _ = GetClientRect(_window, out Rect rectangle);
            IntPtr dc = GetDC(_window);
            uint pixel;
            try
            {
                pixel = GetPixel(dc, rectangle.Right / 2, rectangle.Bottom / 2);
            }
            finally
            {
                _ = ReleaseDC(_window, dc);
            }

            uint r = pixel & 255;
            uint g = (pixel >> 8) & 255;
            uint b = (pixel >> 16) & 255;
            if (pixel != uint.MaxValue && (red ? r > 200 && b < 40 : b > 200 && r < 40) && g < 40)
            {
                return;
            }

            Thread.Sleep(10);
        }

        throw new InvalidOperationException("交換鏈已呈現，但實際視窗中心像素不符。");
    }

    /// <summary>
    /// 釋放交換鏈與測試視窗。
    /// </summary>
    public void Dispose()
    {
        Release(_buffer);
        _buffer = IntPtr.Zero;
        Release(_swapChain);
        _swapChain = IntPtr.Zero;
        if (_window != IntPtr.Zero)
        {
            _ = DestroyWindow(_window);
            _window = IntPtr.Zero;
        }
    }

    /// <summary>
    /// 處理目前執行緒的視窗訊息。
    /// </summary>
    private static void Pump()
    {
        while (PeekMessageW(out Message message, IntPtr.Zero, 0, 0, 1))
        {
            _ = TranslateMessage(in message);
            _ = DispatchMessageW(in message);
        }
    }

    /// <summary>
    /// 取得 Windows SDK 宣告順序中的 COM 函式指標。
    /// </summary>
    /// <param name="instance">
    /// COM 物件。
    /// </param>
    /// <param name="index">
    /// 虛擬函式索引。
    /// </param>
    /// <returns>
    /// 函式指標。
    /// </returns>
    private static void* Slot(IntPtr instance, int index) => ((void**)*(void**)instance)[index];

    /// <summary>
    /// 釋放測試持有的 COM 參考。
    /// </summary>
    /// <param name="instance">
    /// COM 物件。
    /// </param>
    private static void Release(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
        {
            _ = ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(instance, 2))(instance);
        }
    }

    /// <summary>
    /// 對應 DXGI_SWAP_CHAIN_DESC。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SwapChainDescription
    {
        /// <summary>
        /// 對應原生結構的 <c>Width</c> 欄位。
        /// </summary>
        public uint Width;
        /// <summary>
        /// 對應原生結構的 <c>Height</c> 欄位。
        /// </summary>
        public uint Height;
        /// <summary>
        /// 對應原生結構的 <c>RefreshNumerator</c> 欄位。
        /// </summary>
        public uint RefreshNumerator;
        /// <summary>
        /// 對應原生結構的 <c>RefreshDenominator</c> 欄位。
        /// </summary>
        public uint RefreshDenominator;
        /// <summary>
        /// 對應原生結構的 <c>Format</c> 欄位。
        /// </summary>
        public uint Format;
        /// <summary>
        /// 對應原生結構的 <c>ScanlineOrdering</c> 欄位。
        /// </summary>
        public uint ScanlineOrdering;
        /// <summary>
        /// 對應原生結構的 <c>Scaling</c> 欄位。
        /// </summary>
        public uint Scaling;
        /// <summary>
        /// 對應原生結構的 <c>SampleCount</c> 欄位。
        /// </summary>
        public uint SampleCount;
        /// <summary>
        /// 對應原生結構的 <c>SampleQuality</c> 欄位。
        /// </summary>
        public uint SampleQuality;
        /// <summary>
        /// 對應原生結構的 <c>BufferUsage</c> 欄位。
        /// </summary>
        public uint BufferUsage;
        /// <summary>
        /// 對應原生結構的 <c>BufferCount</c> 欄位。
        /// </summary>
        public uint BufferCount;
        /// <summary>
        /// 目標視窗。
        /// </summary>
        public IntPtr OutputWindow;
        /// <summary>
        /// 對應原生結構的 <c>Windowed</c> 欄位。
        /// </summary>
        public uint Windowed;
        /// <summary>
        /// 對應原生結構的 <c>SwapEffect</c> 欄位。
        /// </summary>
        public uint SwapEffect;
        /// <summary>
        /// 對應原生結構的 <c>Flags</c> 欄位。
        /// </summary>
        public uint Flags;
    }

    /// <summary>
    /// 對應 RECT。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        /// <summary>
        /// 對應原生結構的 <c>Left</c> 欄位。
        /// </summary>
        public int Left;
        /// <summary>
        /// 對應原生結構的 <c>Top</c> 欄位。
        /// </summary>
        public int Top;
        /// <summary>
        /// 對應原生結構的 <c>Right</c> 欄位。
        /// </summary>
        public int Right;
        /// <summary>
        /// 對應原生結構的 <c>Bottom</c> 欄位。
        /// </summary>
        public int Bottom;
    }

    /// <summary>
    /// 對應 MSG。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        /// <summary>
        /// 訊息視窗。
        /// </summary>
        public IntPtr Window;
        /// <summary>
        /// 訊息識別碼。
        /// </summary>
        public uint Id;
        /// <summary>
        /// 訊息參數。
        /// </summary>
        public UIntPtr WParam;
        /// <summary>
        /// 訊息參數。
        /// </summary>
        public IntPtr LParam;
        /// <summary>
        /// 對應原生結構的 <c>Time</c> 欄位。
        /// </summary>
        public uint Time;
        /// <summary>
        /// 對應原生結構的 <c>X</c> 欄位。
        /// </summary>
        public uint X;
        /// <summary>
        /// 對應原生結構的 <c>Y</c> 欄位。
        /// </summary>
        public uint Y;
        /// <summary>
        /// 對應原生結構的 <c>Private</c> 欄位。
        /// </summary>
        public uint Private;
    }

    /// <summary>
    /// 建立測試視窗。
    /// </summary>
    /// <param name="exStyle">
    /// 視窗延伸樣式。
    /// </param>
    /// <param name="className">
    /// 視窗類別名稱。
    /// </param>
    /// <param name="title">
    /// 視窗標題。
    /// </param>
    /// <param name="style">
    /// 視窗樣式。
    /// </param>
    /// <param name="x">
    /// 水平座標。
    /// </param>
    /// <param name="y">
    /// 垂直座標。
    /// </param>
    /// <param name="width">
    /// 視窗寬度。
    /// </param>
    /// <param name="height">
    /// 視窗高度。
    /// </param>
    /// <param name="parent">
    /// 父視窗控制代碼。
    /// </param>
    /// <param name="menu">
    /// 功能表控制代碼。
    /// </param>
    /// <param name="instance">
    /// 原生物件指標。
    /// </param>
    /// <param name="parameter">
    /// 建立視窗的附加資料。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string title, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    /// <summary>
    /// 銷毀測試視窗。
    /// </summary>
    /// <param name="window">
    /// 測試視窗控制代碼或呈現物件。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);
    /// <summary>
    /// 讀取視窗訊息。
    /// </summary>
    /// <param name="message">
    /// 視窗訊息資料。
    /// </param>
    /// <param name="window">
    /// 測試視窗控制代碼或呈現物件。
    /// </param>
    /// <param name="min">
    /// 訊息識別碼下限。
    /// </param>
    /// <param name="max">
    /// 訊息識別碼上限。
    /// </param>
    /// <param name="remove">
    /// 是否移除訊息的旗標。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out Message message, IntPtr window, uint min, uint max, uint remove);
    /// <summary>
    /// 轉換視窗訊息。
    /// </summary>
    /// <param name="message">
    /// 視窗訊息資料。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(in Message message);
    /// <summary>
    /// 派送視窗訊息。
    /// </summary>
    /// <param name="message">
    /// 視窗訊息資料。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr DispatchMessageW(in Message message);
    /// <summary>
    /// 等待桌面合成完成。
    /// </summary>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmFlush();
    /// <summary>
    /// 取得視窗用戶端範圍。
    /// </summary>
    /// <param name="window">
    /// 測試視窗控制代碼或呈現物件。
    /// </param>
    /// <param name="rectangle">
    /// 用戶端範圍。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out Rect rectangle);
    /// <summary>
    /// 取得視窗裝置內容。
    /// </summary>
    /// <param name="window">
    /// 測試視窗控制代碼或呈現物件。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetDC(IntPtr window);
    /// <summary>
    /// 釋放視窗裝置內容。
    /// </summary>
    /// <param name="window">
    /// 測試視窗控制代碼或呈現物件。
    /// </param>
    /// <param name="dc">
    /// 視窗裝置內容。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    /// <summary>
    /// 讀取已呈現的像素。
    /// </summary>
    /// <param name="dc">
    /// 視窗裝置內容。
    /// </param>
    /// <param name="x">
    /// 水平座標。
    /// </param>
    /// <param name="y">
    /// 垂直座標。
    /// </param>
    /// <returns>
    /// 原生 API 的執行結果。
    /// </returns>
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern uint GetPixel(IntPtr dc, int x, int y);
}
