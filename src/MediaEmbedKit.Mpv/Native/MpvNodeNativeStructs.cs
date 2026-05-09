using System;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Native
{
    /// <summary>
    /// 對應 libmpv <c>mpv_node</c> 聯集資料。
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NativeMpvNodeValue
    {
        /// <summary>
        /// 字串資料指標。
        /// </summary>
        [FieldOffset(0)]
        public IntPtr String;

        /// <summary>
        /// 布林旗標整數值。
        /// </summary>
        [FieldOffset(0)]
        public int Flag;

        /// <summary>
        /// 64 位元整數值。
        /// </summary>
        [FieldOffset(0)]
        public long Int64;

        /// <summary>
        /// 雙精確度浮點數值。
        /// </summary>
        [FieldOffset(0)]
        public double Double;

        /// <summary>
        /// 節點清單指標。
        /// </summary>
        [FieldOffset(0)]
        public IntPtr List;

        /// <summary>
        /// 位元組陣列指標。
        /// </summary>
        [FieldOffset(0)]
        public IntPtr ByteArray;
    }

    /// <summary>
    /// 對應 libmpv <c>mpv_node</c> 結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeMpvNode
    {
        /// <summary>
        /// 節點值聯集。
        /// </summary>
        public NativeMpvNodeValue Value;

        /// <summary>
        /// 節點資料格式。
        /// </summary>
        public MpvFormat Format;
    }

    /// <summary>
    /// 對應 libmpv <c>mpv_node_list</c> 結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeMpvNodeList
    {
        /// <summary>
        /// 節點數量。
        /// </summary>
        public int Count;

        /// <summary>
        /// 節點值陣列指標。
        /// </summary>
        public IntPtr Values;

        /// <summary>
        /// 節點索引鍵陣列指標。
        /// </summary>
        public IntPtr Keys;
    }

    /// <summary>
    /// 對應 libmpv <c>mpv_byte_array</c> 結構。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeMpvByteArray
    {
        /// <summary>
        /// 位元組資料指標。
        /// </summary>
        public IntPtr Data;

        /// <summary>
        /// 位元組資料長度。
        /// </summary>
        public UIntPtr Size;
    }
}
