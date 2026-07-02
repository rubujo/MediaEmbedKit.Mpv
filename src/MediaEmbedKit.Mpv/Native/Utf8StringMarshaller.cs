using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MediaEmbedKit.Mpv.Native;

/// <summary>
/// 管理傳給 libmpv 的 UTF-8 原生字串緩衝區。
/// </summary>
internal sealed class Utf8String : IDisposable
{
    /// <summary>
    /// 使用指定字串初始化 <see cref="Utf8String"/> 類別的新執行個體。
    /// </summary>
    /// <param name="value">
    /// 要轉換為 UTF-8 原生字串的受控字串。
    /// </param>
    public Utf8String(string? value)
    {
        if (value == null)
        {
            Pointer = IntPtr.Zero;
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Pointer = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, Pointer, bytes.Length);
        Marshal.WriteByte(Pointer, bytes.Length, 0);
    }

    /// <summary>
    /// 取得 UTF-8 原生字串緩衝區指標。
    /// </summary>
    /// <value>
    /// UTF-8 原生字串緩衝區指標。
    /// </value>
    public IntPtr Pointer { get; private set; }

    /// <summary>
    /// 釋放 UTF-8 原生字串緩衝區。
    /// </summary>
    public void Dispose()
    {
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Pointer);
            Pointer = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 解構 <see cref="Utf8String"/> 類別的執行個體。
    /// </summary>
    ~Utf8String()
    {
        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Pointer);
        }
    }
}

/// <summary>
/// 管理傳給 libmpv 的 UTF-8 字串指標陣列。
/// </summary>
internal sealed class Utf8StringArray : IDisposable
{
    /// <summary>
    /// 保存陣列內每個 UTF-8 原生字串。
    /// </summary>
    private readonly Utf8String[] _strings;

    /// <summary>
    /// 使用指定字串陣列初始化 <see cref="Utf8StringArray"/> 類別的新執行個體。
    /// </summary>
    /// <param name="values">
    /// 要轉換為 UTF-8 原生字串陣列的受控字串陣列。
    /// </param>
    public Utf8StringArray(string[] values)
    {
        _strings = new Utf8String[values.Length];
        Pointer = Marshal.AllocHGlobal(IntPtr.Size * (values.Length + 1));

        for (int i = 0; i < values.Length; i++)
        {
            _strings[i] = new Utf8String(values[i]);
            Marshal.WriteIntPtr(Pointer, i * IntPtr.Size, _strings[i].Pointer);
        }

        Marshal.WriteIntPtr(Pointer, values.Length * IntPtr.Size, IntPtr.Zero);
    }

    /// <summary>
    /// 取得 UTF-8 字串指標陣列的原生指標。
    /// </summary>
    /// <value>
    /// 以零結尾的 UTF-8 字串指標陣列。
    /// </value>
    public IntPtr Pointer { get; private set; }

    /// <summary>
    /// 釋放 UTF-8 字串指標陣列與所有字串緩衝區。
    /// </summary>
    public void Dispose()
    {
        if (_strings != null)
        {
            for (int i = 0; i < _strings.Length; i++)
            {
                if (_strings[i] != null)
                {
                    _strings[i].Dispose();
                }
            }
        }

        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Pointer);
            Pointer = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 解構 <see cref="Utf8StringArray"/> 類別的執行個體。
    /// </summary>
    ~Utf8StringArray()
    {
        if (_strings != null)
        {
            for (int i = 0; i < _strings.Length; i++)
            {
                if (_strings[i] != null)
                {
                    _strings[i].Dispose();
                }
            }
        }

        if (Pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(Pointer);
        }
    }
}

/// <summary>
/// 提供 UTF-8 原生字串與受控字串之間的轉換方法。
/// </summary>
internal static unsafe class Utf8StringMarshaller
{
    /// <summary>
    /// 將零結尾 UTF-8 原生字串轉換為受控字串。
    /// </summary>
    /// <param name="pointer">
    /// 零結尾 UTF-8 原生字串指標。
    /// </param>
    /// <returns>
    /// 轉換後的受控字串；指標為零時為 <see langword="null"/>。
    /// </returns>
    public static string? PtrToString(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

#if NET5_0_OR_GREATER
        return Marshal.PtrToStringUTF8(pointer);
#else
        byte* walk = (byte*)pointer;
        while (*walk != 0)
        {
            walk++;
        }

        int length = (int)(walk - (byte*)pointer);
        if (length == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString((byte*)pointer, length);
#endif
    }
}
