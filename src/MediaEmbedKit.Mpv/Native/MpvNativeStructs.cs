using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Native
{
    /// <summary>
    /// 對應 libmpv 原生事件結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEvent
    {
        /// <summary>
        /// libmpv 事件識別碼。
        /// </summary>
        public MpvEventId EventId;
        /// <summary>
        /// libmpv 事件關聯的錯誤碼。
        /// </summary>
        public int Error;
        /// <summary>
        /// libmpv 回覆事件的使用者資料。
        /// </summary>
        public ulong ReplyUserData;
        /// <summary>
        /// libmpv 事件資料指標。
        /// </summary>
        public IntPtr Data;
    }

    /// <summary>
    /// 對應 libmpv 屬性變更事件資料結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventProperty
    {
        /// <summary>
        /// 屬性名稱的 UTF-8 指標。
        /// </summary>
        public IntPtr Name;
        /// <summary>
        /// 屬性值使用的 libmpv 資料格式。
        /// </summary>
        public MpvFormat Format;
        /// <summary>
        /// 屬性值資料指標。
        /// </summary>
        public IntPtr Data;
    }

    /// <summary>
    /// 對應 libmpv 記錄訊息事件資料結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventLogMessage
    {
        /// <summary>
        /// 記錄訊息前置詞指標。
        /// </summary>
        public IntPtr Prefix;
        /// <summary>
        /// 記錄訊息等級文字指標。
        /// </summary>
        public IntPtr Level;
        /// <summary>
        /// 記錄訊息內容指標。
        /// </summary>
        public IntPtr Text;
        /// <summary>
        /// 記錄訊息等級列舉值。
        /// </summary>
        public MpvLogLevel LogLevel;
    }

    /// <summary>
    /// 對應 libmpv 播放項目結束事件資料結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventEndFile
    {
        /// <summary>
        /// 播放項目結束原因。
        /// </summary>
        public MpvEndFileReason Reason;
        /// <summary>
        /// 播放項目結束時的 libmpv 錯誤碼。
        /// </summary>
        public int Error;
        /// <summary>
        /// 播放清單項目識別碼。
        /// </summary>
        public long PlaylistEntryId;
        /// <summary>
        /// 插入播放清單項目的起始識別碼。
        /// </summary>
        public long PlaylistInsertId;
        /// <summary>
        /// 插入播放清單的項目數量。
        /// </summary>
        public int PlaylistInsertNumEntries;
    }

    /// <summary>
    /// 對應 libmpv 開始播放項目事件資料結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventStartFile
    {
        /// <summary>
        /// 播放清單項目識別碼。
        /// </summary>
        public long PlaylistEntryId;
    }

    /// <summary>
    /// 對應 libmpv 用戶端訊息事件資料結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventClientMessage
    {
        /// <summary>
        /// 訊息引數數量。
        /// </summary>
        public int ArgumentCount;

        /// <summary>
        /// 訊息引數陣列指標。
        /// </summary>
        public IntPtr Arguments;
    }

    /// <summary>
    /// 對應 libmpv 掛鉤事件資料結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventHook
    {
        /// <summary>
        /// 掛鉤名稱指標。
        /// </summary>
        public IntPtr Name;

        /// <summary>
        /// 掛鉤識別碼。
        /// </summary>
        public ulong Id;
    }

    /// <summary>
    /// 對應 libmpv 命令回覆事件資料結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MpvEventCommand
    {
        /// <summary>
        /// 命令回傳資料節點。
        /// </summary>
        public NativeMpvNode Result;
    }
}
