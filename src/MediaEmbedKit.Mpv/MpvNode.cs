using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using MediaEmbedKit.Mpv.Native;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 表示 libmpv <c>mpv_node</c> 的受控值。
/// </summary>
public sealed class MpvNode
{
    /// <summary>
    /// 初始化 <see cref="MpvNode"/> 類別的新執行個體。
    /// </summary>
    /// <param name="format">節點資料格式。</param>
    /// <param name="value">節點保存的受控值。</param>
    private MpvNode(MpvFormat format, object? value)
    {
        Format = format;
        Value = value;
    }

    /// <summary>
    /// 取得節點資料格式。
    /// </summary>
    /// <value>libmpv 節點資料格式。</value>
    public MpvFormat Format { get; private set; }

    /// <summary>
    /// 取得節點保存的受控值。
    /// </summary>
    /// <value>依 <see cref="Format"/> 決定型別的受控值。</value>
    public object? Value { get; private set; }

    /// <summary>
    /// 取得目前節點是否為空節點。
    /// </summary>
    /// <value>節點格式為 <see cref="MpvFormat.None"/> 時為 <see langword="true"/>。</value>
    public bool IsNone
    {
        get { return Format == MpvFormat.None; }
    }

    /// <summary>
    /// 建立空節點。
    /// </summary>
    /// <returns>空節點。</returns>
    public static MpvNode None()
    {
        return new MpvNode(MpvFormat.None, null);
    }

    /// <summary>
    /// 建立字串節點。
    /// </summary>
    /// <param name="value">節點字串值。</param>
    /// <returns>字串節點。</returns>
    public static MpvNode FromString(string value)
    {
        return new MpvNode(MpvFormat.String, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>
    /// 建立布林旗標節點。
    /// </summary>
    /// <param name="value">節點布林值。</param>
    /// <returns>布林旗標節點。</returns>
    public static MpvNode FromFlag(bool value)
    {
        return new MpvNode(MpvFormat.Flag, value);
    }

    /// <summary>
    /// 建立 64 位元整數節點。
    /// </summary>
    /// <param name="value">節點整數值。</param>
    /// <returns>64 位元整數節點。</returns>
    public static MpvNode FromInt64(long value)
    {
        return new MpvNode(MpvFormat.Int64, value);
    }

    /// <summary>
    /// 建立雙精確度浮點數節點。
    /// </summary>
    /// <param name="value">節點浮點數值。</param>
    /// <returns>雙精確度浮點數節點。</returns>
    public static MpvNode FromDouble(double value)
    {
        return new MpvNode(MpvFormat.Double, value);
    }

    /// <summary>
    /// 建立節點陣列。
    /// </summary>
    /// <param name="items">陣列項目。</param>
    /// <returns>節點陣列。</returns>
    public static MpvNode FromArray(IEnumerable<MpvNode> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        return new MpvNode(MpvFormat.NodeArray, new ReadOnlyCollection<MpvNode>(items.ToArray()));
    }

    /// <summary>
    /// 建立節點對應。
    /// </summary>
    /// <param name="items">以字串索引鍵對應到節點值的集合。</param>
    /// <returns>節點對應。</returns>
    public static MpvNode FromMap(IDictionary<string, MpvNode> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        return new MpvNode(MpvFormat.NodeMap, new ReadOnlyDictionary<string, MpvNode>(items));
    }

    /// <summary>
    /// 建立位元組陣列節點。
    /// </summary>
    /// <param name="bytes">節點位元組資料。</param>
    /// <returns>位元組陣列節點。</returns>
    public static MpvNode FromByteArray(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return new MpvNode(MpvFormat.ByteArray, bytes.ToArray());
    }

    /// <summary>
    /// 將節點值讀取為字串。
    /// </summary>
    /// <returns>節點字串值；節點不是字串時會傳回 <see langword="null"/>。</returns>
    public string? AsString()
    {
        return Value as string;
    }

    /// <summary>
    /// 將節點值讀取為布林值。
    /// </summary>
    /// <returns>節點布林值；節點不是布林值時會傳回 <see langword="false"/>。</returns>
    public bool AsBoolean()
    {
        return Value is bool flag && flag;
    }

    /// <summary>
    /// 將節點值讀取為 64 位元整數。
    /// </summary>
    /// <returns>節點整數值；節點不是整數時會傳回 0。</returns>
    public long AsInt64()
    {
        return Value is long integer ? integer : 0;
    }

    /// <summary>
    /// 將節點值讀取為雙精確度浮點數。
    /// </summary>
    /// <returns>節點浮點數值；節點不是浮點數時會傳回 0。</returns>
    public double AsDouble()
    {
        return Value is double number ? number : 0;
    }

    /// <summary>
    /// 將節點值讀取為節點陣列。
    /// </summary>
    /// <returns>節點陣列；節點不是陣列時會傳回空陣列。</returns>
    public IReadOnlyList<MpvNode> AsArray()
    {
        return Value as IReadOnlyList<MpvNode> ?? Array.Empty<MpvNode>();
    }

    /// <summary>
    /// 將節點值讀取為節點對應。
    /// </summary>
    /// <returns>節點對應；節點不是對應時會傳回空字典。</returns>
    public IReadOnlyDictionary<string, MpvNode> AsMap()
    {
        return Value as IReadOnlyDictionary<string, MpvNode> ?? new ReadOnlyDictionary<string, MpvNode>(new Dictionary<string, MpvNode>());
    }

    /// <summary>
    /// 將節點值讀取為位元組陣列。
    /// </summary>
    /// <returns>節點位元組陣列；節點不是位元組陣列時會傳回空陣列。</returns>
    public byte[] AsByteArray()
    {
        byte[]? bytes = Value as byte[];
        return bytes == null ? Array.Empty<byte>() : bytes.ToArray();
    }

    /// <summary>
    /// 嘗試從節點對應取得指定索引鍵的值。
    /// </summary>
    /// <param name="key">要查詢的節點索引鍵。</param>
    /// <param name="value">找到時接收對應的節點值；找不到時接收空節點。</param>
    /// <returns>找到指定索引鍵時為 <see langword="true"/>。</returns>
    public bool TryGetValue(string key, out MpvNode value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        IReadOnlyDictionary<string, MpvNode> map = AsMap();
        MpvNode? foundValue;
        if (map.TryGetValue(key, out foundValue) && foundValue != null)
        {
            value = foundValue;
            return true;
        }

        value = None();
        return false;
    }

    /// <summary>
    /// 從節點對應取得指定索引鍵的值。
    /// </summary>
    /// <param name="key">要查詢的節點索引鍵。</param>
    /// <returns>找到時為對應的節點值；找不到時為空節點。</returns>
    public MpvNode GetValueOrNone(string key)
    {
        MpvNode value;
        return TryGetValue(key, out value) ? value : None();
    }

    /// <summary>
    /// 從原生 libmpv 節點轉換為受控節點。
    /// </summary>
    /// <param name="node">原生 libmpv 節點。</param>
    /// <returns>轉換後的受控節點。</returns>
    internal static MpvNode FromNative(NativeMpvNode node)
    {
        switch (node.Format)
        {
            case MpvFormat.String:
            case MpvFormat.OsdString:
                return new MpvNode(node.Format, Utf8StringMarshaller.PtrToString(node.Value.String) ?? string.Empty);
            case MpvFormat.Flag:
                return FromFlag(node.Value.Flag != 0);
            case MpvFormat.Int64:
                return FromInt64(node.Value.Int64);
            case MpvFormat.Double:
                return FromDouble(node.Value.Double);
            case MpvFormat.NodeArray:
                return FromArray(DecodeArray(node.Value.List));
            case MpvFormat.NodeMap:
                return FromMap(DecodeMap(node.Value.List));
            case MpvFormat.ByteArray:
                return FromByteArray(DecodeByteArray(node.Value.ByteArray));
            default:
                return None();
        }
    }

    /// <summary>
    /// 將原生節點陣列轉換為受控清單。
    /// </summary>
    /// <param name="listPointer">原生節點清單指標。</param>
    /// <returns>受控節點清單。</returns>
    private static IReadOnlyList<MpvNode> DecodeArray(IntPtr listPointer)
    {
        if (listPointer == IntPtr.Zero)
        {
            return Array.Empty<MpvNode>();
        }

        NativeMpvNodeList list = (NativeMpvNodeList)Marshal.PtrToStructure(listPointer, typeof(NativeMpvNodeList))!;
        List<MpvNode> result = new List<MpvNode>(Math.Max(0, list.Count));
        int size = Marshal.SizeOf(typeof(NativeMpvNode));
        for (int i = 0; i < list.Count; i++)
        {
            NativeMpvNode item = (NativeMpvNode)Marshal.PtrToStructure(IntPtr.Add(list.Values, i * size), typeof(NativeMpvNode))!;
            result.Add(FromNative(item));
        }

        return result;
    }

    /// <summary>
    /// 將原生節點對應轉換為受控字典。
    /// </summary>
    /// <param name="listPointer">原生節點清單指標。</param>
    /// <returns>受控節點字典。</returns>
    private static IDictionary<string, MpvNode> DecodeMap(IntPtr listPointer)
    {
        if (listPointer == IntPtr.Zero)
        {
            return new Dictionary<string, MpvNode>();
        }

        NativeMpvNodeList list = (NativeMpvNodeList)Marshal.PtrToStructure(listPointer, typeof(NativeMpvNodeList))!;
        Dictionary<string, MpvNode> result = new Dictionary<string, MpvNode>(StringComparer.Ordinal);
        int size = Marshal.SizeOf(typeof(NativeMpvNode));
        for (int i = 0; i < list.Count; i++)
        {
            IntPtr keyPointer = Marshal.ReadIntPtr(list.Keys, i * IntPtr.Size);
            string key = Utf8StringMarshaller.PtrToString(keyPointer) ?? string.Empty;
            NativeMpvNode value = (NativeMpvNode)Marshal.PtrToStructure(IntPtr.Add(list.Values, i * size), typeof(NativeMpvNode))!;
            result[key] = FromNative(value);
        }

        return result;
    }

    /// <summary>
    /// 將原生位元組陣列轉換為受控位元組陣列。
    /// </summary>
    /// <param name="byteArrayPointer">原生位元組陣列指標。</param>
    /// <returns>受控位元組陣列。</returns>
    private static byte[] DecodeByteArray(IntPtr byteArrayPointer)
    {
        if (byteArrayPointer == IntPtr.Zero)
        {
            return Array.Empty<byte>();
        }

        NativeMpvByteArray byteArray = (NativeMpvByteArray)Marshal.PtrToStructure(byteArrayPointer, typeof(NativeMpvByteArray))!;
        int length = checked((int)byteArray.Size.ToUInt64());
        byte[] bytes = new byte[length];
        if (length > 0)
        {
            Marshal.Copy(byteArray.Data, bytes, 0, length);
        }

        return bytes;
    }
}
