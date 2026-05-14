using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv.Native;

/// <summary>
/// 提供 <see cref="MpvNode"/> 與原生 <c>mpv_node</c> 之間的配置轉換。
/// </summary>
internal sealed class MpvNodeAllocation : IDisposable
{
    /// <summary>
    /// 保存需要一併釋放的子配置。
    /// </summary>
    private readonly List<IDisposable> _children = new List<IDisposable>();
    /// <summary>
    /// 保存此配置直接擁有的原生記憶體指標。
    /// </summary>
    private readonly List<IntPtr> _allocations = new List<IntPtr>();
    /// <summary>
    /// 表示目前配置是否已釋放。
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="MpvNodeAllocation"/> 類別的新執行個體。
    /// </summary>
    /// <param name="node">受控節點。</param>
    public MpvNodeAllocation(MpvNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        NativeNode = BuildNode(node);
    }

    /// <summary>
    /// 取得可傳給 libmpv 的原生節點。
    /// </summary>
    /// <value>原生節點。</value>
    public NativeMpvNode NativeNode { get; private set; }

    /// <summary>
    /// 釋放此節點配置持有的原生記憶體。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].Dispose();
        }

        for (int i = 0; i < _allocations.Count; i++)
        {
            if (_allocations[i] != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_allocations[i]);
            }
        }
    }

    /// <summary>
    /// 建立指定受控節點的原生表示。
    /// </summary>
    /// <param name="node">要轉換的受控節點。</param>
    /// <returns>原生節點。</returns>
    private NativeMpvNode BuildNode(MpvNode node)
    {
        NativeMpvNode native = new NativeMpvNode
        {
            Format = node.Format
        };

        switch (node.Format)
        {
            case MpvFormat.String:
                Utf8String text = new Utf8String((string?)node.Value ?? string.Empty);
                _children.Add(text);
                native.Value.String = text.Pointer;
                break;
            case MpvFormat.Flag:
                native.Value.Flag = (bool)node.Value! ? 1 : 0;
                break;
            case MpvFormat.Int64:
                native.Value.Int64 = (long)node.Value!;
                break;
            case MpvFormat.Double:
                native.Value.Double = (double)node.Value!;
                break;
            case MpvFormat.NodeArray:
                native.Value.List = BuildArray((IReadOnlyList<MpvNode>)node.Value!);
                break;
            case MpvFormat.NodeMap:
                native.Value.List = BuildMap((IReadOnlyDictionary<string, MpvNode>)node.Value!);
                break;
            case MpvFormat.ByteArray:
                native.Value.ByteArray = BuildByteArray((byte[])node.Value!);
                break;
        }

        return native;
    }

    /// <summary>
    /// 建立節點陣列的原生清單。
    /// </summary>
    /// <param name="items">受控節點項目。</param>
    /// <returns>原生節點清單指標。</returns>
    private IntPtr BuildArray(IReadOnlyList<MpvNode> items)
    {
        IntPtr values = BuildValues(items);
        NativeMpvNodeList list = new NativeMpvNodeList
        {
            Count = items.Count,
            Values = values,
            Keys = IntPtr.Zero
        };
        return AllocStruct(list);
    }

    /// <summary>
    /// 建立節點對應的原生清單。
    /// </summary>
    /// <param name="items">受控節點對應。</param>
    /// <returns>原生節點清單指標。</returns>
    private IntPtr BuildMap(IReadOnlyDictionary<string, MpvNode> items)
    {
        List<string> keysSource = new List<string>(items.Count);
        List<MpvNode> valuesSource = new List<MpvNode>(items.Count);
        foreach (KeyValuePair<string, MpvNode> item in items)
        {
            keysSource.Add(item.Key);
            valuesSource.Add(item.Value);
        }

        IntPtr values = BuildValues(valuesSource);
        IntPtr keys = Marshal.AllocHGlobal(IntPtr.Size * items.Count);
        _allocations.Add(keys);

        for (int index = 0; index < keysSource.Count; index++)
        {
            Utf8String keyString = new Utf8String(keysSource[index]);
            _children.Add(keyString);
            Marshal.WriteIntPtr(keys, index * IntPtr.Size, keyString.Pointer);
        }

        NativeMpvNodeList list = new NativeMpvNodeList
        {
            Count = items.Count,
            Values = values,
            Keys = keys
        };
        return AllocStruct(list);
    }

    /// <summary>
    /// 建立節點值陣列。
    /// </summary>
    /// <param name="items">受控節點項目。</param>
    /// <returns>原生節點陣列指標。</returns>
    private IntPtr BuildValues(IReadOnlyList<MpvNode> items)
    {
        int size = Marshal.SizeOf<NativeMpvNode>();
        IntPtr values = Marshal.AllocHGlobal(size * items.Count);
        _allocations.Add(values);

        for (int i = 0; i < items.Count; i++)
        {
            MpvNodeAllocation child = new MpvNodeAllocation(items[i]);
            _children.Add(child);
            Marshal.StructureToPtr(child.NativeNode, IntPtr.Add(values, i * size), false);
        }

        return values;
    }

    /// <summary>
    /// 建立位元組陣列的原生表示。
    /// </summary>
    /// <param name="bytes">受控位元組陣列。</param>
    /// <returns>原生位元組陣列指標。</returns>
    private IntPtr BuildByteArray(byte[] bytes)
    {
        IntPtr data = Marshal.AllocHGlobal(bytes.Length);
        _allocations.Add(data);
        if (bytes.Length > 0)
        {
            Marshal.Copy(bytes, 0, data, bytes.Length);
        }

        NativeMpvByteArray byteArray = new NativeMpvByteArray
        {
            Data = data,
            Size = new UIntPtr(unchecked((ulong)bytes.Length))
        };
        return AllocStruct(byteArray);
    }

    /// <summary>
    /// 配置原生記憶體並寫入指定結構。
    /// </summary>
    /// <typeparam name="T">要寫入的結構型別。</typeparam>
    /// <param name="value">要寫入的結構值。</param>
    /// <returns>包含指定結構的原生記憶體指標。</returns>
    private IntPtr AllocStruct<T>(T value) where T : struct
    {
        IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        _allocations.Add(pointer);
        Marshal.StructureToPtr(value, pointer, false);
        return pointer;
    }
}
