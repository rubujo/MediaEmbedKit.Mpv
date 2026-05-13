using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Native;

/// <summary>
/// 宣告 libmpv 原生 API 的 P/Invoke 進入點。
/// </summary>
internal static class MpvNative
{
    /// <summary>
    /// P/Invoke 使用的 libmpv 程式庫名稱。
    /// </summary>
    private const string LibraryName = "libmpv-2";

    /// <summary>
    /// 接收 libmpv 事件喚醒通知的委派型別。
    /// </summary>
    /// <param name="context">呼叫端註冊的回呼內容指標。</param>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void MpvWakeupCallback(IntPtr context);

    /// <summary>
    /// 取得 libmpv 用戶端 API 版本。
    /// </summary>
    /// <returns>libmpv 用戶端 API 版本值。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern uint mpv_client_api_version();

    /// <summary>
    /// 取得 libmpv 錯誤碼對應的原生錯誤字串。
    /// </summary>
    /// <param name="error">libmpv 錯誤碼。</param>
    /// <returns>零結尾 UTF-8 錯誤字串指標。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern IntPtr mpv_error_string(int error);

    /// <summary>
    /// 釋放由 libmpv 配置的記憶體。
    /// </summary>
    /// <param name="data">要釋放的原生資料指標。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_free(IntPtr data);

    /// <summary>
    /// 取得 libmpv 用戶端名稱。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <returns>零結尾 UTF-8 用戶端名稱指標。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_client_name(IntPtr ctx);

    /// <summary>
    /// 取得 libmpv 用戶端識別碼。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <returns>libmpv 用戶端識別碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern long mpv_client_id(IntPtr ctx);

    /// <summary>
    /// 建立新的 libmpv 用戶端。
    /// </summary>
    /// <returns>新建立的 libmpv 用戶端控制代碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_create();

    /// <summary>
    /// 初始化指定的 libmpv 用戶端。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_initialize(IntPtr ctx);

    /// <summary>
    /// 銷毀指定的 libmpv 用戶端。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_destroy(IntPtr ctx);

    /// <summary>
    /// 終止並銷毀指定的 libmpv 用戶端。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_terminate_destroy(IntPtr ctx);

    /// <summary>
    /// 從既有 libmpv 用戶端建立新的強參考用戶端。
    /// </summary>
    /// <param name="ctx">來源 libmpv 用戶端控制代碼。</param>
    /// <param name="name">新用戶端名稱指標。</param>
    /// <returns>新建立的 libmpv 用戶端控制代碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_create_client(IntPtr ctx, IntPtr name);

    /// <summary>
    /// 從既有 libmpv 用戶端建立新的弱參考用戶端。
    /// </summary>
    /// <param name="ctx">來源 libmpv 用戶端控制代碼。</param>
    /// <param name="name">新用戶端名稱指標。</param>
    /// <returns>新建立的弱參考 libmpv 用戶端控制代碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_create_weak_client(IntPtr ctx, IntPtr name);

    /// <summary>
    /// 載入 mpv 設定檔。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="filename">設定檔路徑 UTF-8 指標。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_load_config_file(IntPtr ctx, IntPtr filename);

    /// <summary>
    /// 取得 libmpv 單調時間。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <returns>以奈秒表示的 libmpv 時間。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern ulong mpv_get_time_ns(IntPtr ctx);

    /// <summary>
    /// 取得 libmpv 單調時間。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <returns>以微秒表示的 libmpv 時間。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern long mpv_get_time_us(IntPtr ctx);

    /// <summary>
    /// 釋放由 libmpv 配置的節點內容。
    /// </summary>
    /// <param name="node">要釋放內容的節點。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_free_node_contents(ref NativeMpvNode node);

    /// <summary>
    /// 以整數旗標格式設定 libmpv 選項。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">選項名稱 UTF-8 指標。</param>
    /// <param name="format">選項資料格式。</param>
    /// <param name="data">選項值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_option(IntPtr ctx, IntPtr name, MpvFormat format, ref int data);

    /// <summary>
    /// 以 64 位元整數格式設定 libmpv 選項。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">選項名稱 UTF-8 指標。</param>
    /// <param name="format">選項資料格式。</param>
    /// <param name="data">選項值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_option(IntPtr ctx, IntPtr name, MpvFormat format, ref long data);

    /// <summary>
    /// 以雙精確度浮點數格式設定 libmpv 選項。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">選項名稱 UTF-8 指標。</param>
    /// <param name="format">選項資料格式。</param>
    /// <param name="data">選項值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_option(IntPtr ctx, IntPtr name, MpvFormat format, ref double data);

    /// <summary>
    /// 以節點格式設定 libmpv 選項。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">選項名稱 UTF-8 指標。</param>
    /// <param name="format">選項資料格式。</param>
    /// <param name="data">選項節點參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_option(IntPtr ctx, IntPtr name, MpvFormat format, ref NativeMpvNode data);

    /// <summary>
    /// 以字串設定 libmpv 選項。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">選項名稱 UTF-8 指標。</param>
    /// <param name="data">選項值 UTF-8 指標。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_option_string(IntPtr ctx, IntPtr name, IntPtr data);

    /// <summary>
    /// 使用引數陣列同步執行 libmpv 命令。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="args">零結尾 UTF-8 字串指標陣列。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_command(IntPtr ctx, IntPtr args);

    /// <summary>
    /// 使用節點資料同步執行 libmpv 命令。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="args">命令引數節點參考。</param>
    /// <param name="result">接收命令回傳節點的輸出變數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_command_node(IntPtr ctx, ref NativeMpvNode args, out NativeMpvNode result);

    /// <summary>
    /// 使用引數陣列同步執行 libmpv 命令並取回節點結果。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="args">零結尾 UTF-8 字串指標陣列。</param>
    /// <param name="result">接收命令回傳節點的輸出變數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_command_ret(IntPtr ctx, IntPtr args, out NativeMpvNode result);

    /// <summary>
    /// 使用命令字串同步執行 libmpv 命令。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="args">命令字串 UTF-8 指標。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_command_string(IntPtr ctx, IntPtr args);

    /// <summary>
    /// 使用引數陣列非同步執行 libmpv 命令。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">回覆事件使用的使用者資料。</param>
    /// <param name="args">零結尾 UTF-8 字串指標陣列。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_command_async(IntPtr ctx, ulong replyUserData, IntPtr args);

    /// <summary>
    /// 使用節點資料非同步執行 libmpv 命令。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">回覆事件使用的使用者資料。</param>
    /// <param name="args">命令引數節點參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_command_node_async(IntPtr ctx, ulong replyUserData, ref NativeMpvNode args);

    /// <summary>
    /// 中止指定的 libmpv 非同步命令。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">要中止的非同步命令使用者資料。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_abort_async_command(IntPtr ctx, ulong replyUserData);

    /// <summary>
    /// 以 UTF-8 字串設定 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="data">屬性值 UTF-8 指標。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property_string(IntPtr ctx, IntPtr name, IntPtr data);

    /// <summary>
    /// 以整數旗標格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property(IntPtr ctx, IntPtr name, MpvFormat format, ref int data);

    /// <summary>
    /// 以 64 位元整數格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property(IntPtr ctx, IntPtr name, MpvFormat format, ref long data);

    /// <summary>
    /// 以雙精確度浮點數格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property(IntPtr ctx, IntPtr name, MpvFormat format, ref double data);

    /// <summary>
    /// 以節點格式設定 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性節點參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property(IntPtr ctx, IntPtr name, MpvFormat format, ref NativeMpvNode data);

    /// <summary>
    /// 刪除指定的 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_del_property(IntPtr ctx, IntPtr name);

    /// <summary>
    /// 以整數旗標格式取得 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">接收屬性值的輸出變數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_get_property(IntPtr ctx, IntPtr name, MpvFormat format, out int data);

    /// <summary>
    /// 以 64 位元整數格式取得 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">接收屬性值的輸出變數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_get_property(IntPtr ctx, IntPtr name, MpvFormat format, out long data);

    /// <summary>
    /// 以雙精確度浮點數格式取得 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">接收屬性值的輸出變數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_get_property(IntPtr ctx, IntPtr name, MpvFormat format, out double data);

    /// <summary>
    /// 以節點格式取得 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">接收屬性節點的輸出變數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_get_property(IntPtr ctx, IntPtr name, MpvFormat format, out NativeMpvNode data);

    /// <summary>
    /// 以字串格式取得 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <returns>由 libmpv 配置的 UTF-8 字串指標。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_get_property_string(IntPtr ctx, IntPtr name);

    /// <summary>
    /// 取得適合螢幕顯示的 libmpv 屬性字串。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <returns>由 libmpv 配置的螢幕顯示字串指標。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_get_property_osd_string(IntPtr ctx, IntPtr name);

    /// <summary>
    /// 非同步取得 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">回覆事件使用的使用者資料。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_get_property_async(IntPtr ctx, ulong replyUserData, IntPtr name, MpvFormat format);

    /// <summary>
    /// 非同步設定整數旗標格式的 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">回覆事件使用的使用者資料。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property_async(IntPtr ctx, ulong replyUserData, IntPtr name, MpvFormat format, ref int data);

    /// <summary>
    /// 非同步設定 64 位元整數格式的 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">回覆事件使用的使用者資料。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property_async(IntPtr ctx, ulong replyUserData, IntPtr name, MpvFormat format, ref long data);

    /// <summary>
    /// 非同步設定雙精確度浮點數格式的 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">回覆事件使用的使用者資料。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性值參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property_async(IntPtr ctx, ulong replyUserData, IntPtr name, MpvFormat format, ref double data);

    /// <summary>
    /// 非同步設定節點格式的 libmpv 屬性。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">回覆事件使用的使用者資料。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <param name="data">屬性節點參考。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_set_property_async(IntPtr ctx, ulong replyUserData, IntPtr name, MpvFormat format, ref NativeMpvNode data);

    /// <summary>
    /// 訂閱指定 libmpv 屬性的變更通知。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">屬性變更事件使用的使用者資料。</param>
    /// <param name="name">屬性名稱 UTF-8 指標。</param>
    /// <param name="format">屬性資料格式。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_observe_property(IntPtr ctx, ulong replyUserData, IntPtr name, MpvFormat format);

    /// <summary>
    /// 取消指定 libmpv 屬性的變更通知。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="registeredReplyUserData">先前註冊屬性觀察時使用的使用者資料。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_unobserve_property(IntPtr ctx, ulong registeredReplyUserData);

    /// <summary>
    /// 取得 libmpv 事件識別碼對應的事件名稱。
    /// </summary>
    /// <param name="eventId">libmpv 事件識別碼。</param>
    /// <returns>零結尾 UTF-8 事件名稱指標。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_event_name(MpvEventId eventId);

    /// <summary>
    /// 將 libmpv 事件轉換為節點對應。
    /// </summary>
    /// <param name="destination">接收事件節點的輸出變數。</param>
    /// <param name="source">來源事件指標。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_event_to_node(out NativeMpvNode destination, IntPtr source);

    /// <summary>
    /// 啟用或停用指定的 libmpv 事件。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="eventId">libmpv 事件識別碼。</param>
    /// <param name="enable">啟用事件時為 1，停用時為 0。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_request_event(IntPtr ctx, MpvEventId eventId, int enable);

    /// <summary>
    /// 訂閱指定最低等級以上的 libmpv 記錄訊息。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="minLevel">最低記錄等級 UTF-8 指標。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_request_log_messages(IntPtr ctx, IntPtr minLevel);

    /// <summary>
    /// 等待下一個 libmpv 事件。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="timeout">等待事件的秒數，負值代表無限等待。</param>
    /// <returns>libmpv 事件指標。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    /// <summary>
    /// 喚醒正在等待事件的 libmpv 用戶端。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_wakeup(IntPtr ctx);

    /// <summary>
    /// 設定 libmpv 事件喚醒通知回呼。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="callback">事件喚醒通知回呼。</param>
    /// <param name="callbackContext">傳回回呼的內容指標。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_set_wakeup_callback(IntPtr ctx, MpvWakeupCallback? callback, IntPtr callbackContext);

    /// <summary>
    /// 等待 libmpv 非同步要求完成。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_wait_async_requests(IntPtr ctx);

    /// <summary>
    /// 取得 libmpv 事件喚醒 pipe 檔案描述元。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <returns>喚醒 pipe 檔案描述元；失敗時為負值。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_get_wakeup_pipe(IntPtr ctx);

    /// <summary>
    /// 新增 libmpv 掛鉤。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="replyUserData">掛鉤事件使用的使用者資料。</param>
    /// <param name="name">掛鉤名稱 UTF-8 指標。</param>
    /// <param name="priority">掛鉤優先順序。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_hook_add(IntPtr ctx, ulong replyUserData, IntPtr name, int priority);

    /// <summary>
    /// 繼續指定的 libmpv 掛鉤。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="id">libmpv 掛鉤識別碼。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_hook_continue(IntPtr ctx, ulong id);

    /// <summary>
    /// 註冊 libmpv 自訂唯讀串流通訊協定。
    /// </summary>
    /// <param name="ctx">libmpv 用戶端控制代碼。</param>
    /// <param name="protocol">不含 <c>://</c> 的通訊協定前置詞 UTF-8 指標。</param>
    /// <param name="userData">傳回開啟回呼的使用者資料指標。</param>
    /// <param name="openCallback">建立串流執行個體的開啟回呼。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_stream_cb_add_ro(IntPtr ctx, IntPtr protocol, IntPtr userData, MpvStreamOpenCallback openCallback);

    /// <summary>
    /// 建立 libmpv render API 內容。
    /// </summary>
    /// <param name="result">接收 render API 內容指標的輸出變數。</param>
    /// <param name="mpv">libmpv 用戶端控制代碼。</param>
    /// <param name="parameters">render API 建立參數陣列。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_render_context_create(out IntPtr result, IntPtr mpv, [In] MpvRenderParam[] parameters);

    /// <summary>
    /// 設定 libmpv render API 內容參數。
    /// </summary>
    /// <param name="ctx">render API 內容指標。</param>
    /// <param name="parameter">要設定的 render API 參數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_render_context_set_parameter(IntPtr ctx, MpvRenderParam parameter);

    /// <summary>
    /// 取得 libmpv render API 內容資訊。
    /// </summary>
    /// <param name="ctx">render API 內容指標。</param>
    /// <param name="parameter">要查詢的 render API 參數。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_render_context_get_info(IntPtr ctx, MpvRenderParam parameter);

    /// <summary>
    /// 設定 libmpv render API 更新通知回呼。
    /// </summary>
    /// <param name="ctx">render API 內容指標。</param>
    /// <param name="callback">更新通知回呼。</param>
    /// <param name="callbackContext">傳回回呼的內容指標。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_render_context_set_update_callback(IntPtr ctx, MpvRenderUpdateCallback? callback, IntPtr callbackContext);

    /// <summary>
    /// 讀取並清除 libmpv render API 更新旗標。
    /// </summary>
    /// <param name="ctx">render API 內容指標。</param>
    /// <returns>render API 更新旗標值。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern ulong mpv_render_context_update(IntPtr ctx);

    /// <summary>
    /// 要求 libmpv render API 繪製目前影格。
    /// </summary>
    /// <param name="ctx">render API 內容指標。</param>
    /// <param name="parameters">render API 繪製參數陣列。</param>
    /// <returns>libmpv 錯誤碼。</returns>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int mpv_render_context_render(IntPtr ctx, [In] MpvRenderParam[] parameters);

    /// <summary>
    /// 通知 libmpv 呼叫端已交換顯示緩衝區。
    /// </summary>
    /// <param name="ctx">render API 內容指標。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_render_context_report_swap(IntPtr ctx);

    /// <summary>
    /// 釋放 libmpv render API 內容。
    /// </summary>
    /// <param name="ctx">render API 內容指標。</param>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void mpv_render_context_free(IntPtr ctx);

    /// <summary>
    /// 將 libmpv 錯誤碼轉換為受控字串。
    /// </summary>
    /// <param name="error">libmpv 錯誤碼。</param>
    /// <returns>錯誤碼對應的訊息文字。</returns>
    internal static string GetErrorString(int error)
    {
        return Utf8StringMarshaller.PtrToString(mpv_error_string(error)) ?? "unknown mpv error";
    }

    /// <summary>
    /// 將 libmpv 事件識別碼轉換為事件名稱。
    /// </summary>
    /// <param name="eventId">libmpv 事件識別碼。</param>
    /// <returns>事件識別碼對應的名稱。</returns>
    internal static string GetEventName(MpvEventId eventId)
    {
        return Utf8StringMarshaller.PtrToString(mpv_event_name(eventId)) ?? eventId.ToString();
    }
}
