using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MediaEmbedKit.Mpv;

/// <summary>
/// 提供載入 libmpv 原生程式庫的共用邏輯。
/// </summary>
public static class MpvLibraryLoader
{
    /// <summary>
    /// Windows 平台使用的 libmpv 檔案名稱。
    /// </summary>
    private const string WindowsLibraryFileName = "libmpv-2.dll";
    /// <summary>
    /// 保護載入狀態集合的同步物件。
    /// </summary>
    private static readonly object SyncRoot = new object();
    /// <summary>
    /// 保存已載入原生程式庫的控制代碼。
    /// </summary>
    private static readonly List<IntPtr> LoadedLibraries = new List<IntPtr>();
    /// <summary>
    /// 保存已載入原生程式庫的完整路徑。
    /// </summary>
    private static readonly List<string> LoadedLibraryPaths = new List<string>();

    /// <summary>
    /// 取得目前處理序是否已載入 libmpv。
    /// </summary>
    /// <value>已載入至少一個 libmpv 原生程式庫時為 <see langword="true"/>。</value>
    public static bool IsLoaded
    {
        get
        {
            lock (SyncRoot)
            {
                return LoadedLibraries.Count > 0;
            }
        }
    }

    /// <summary>
    /// 載入指定或自動解析的 libmpv 原生程式庫。
    /// </summary>
    /// <param name="libraryPath">libmpv 檔案路徑或包含 libmpv 的資料夾；未指定時使用預設搜尋邏輯。</param>
    public static void Load(string? libraryPath = null)
    {
        string? resolved = ResolveLibraryPath(libraryPath);
        if (string.IsNullOrEmpty(resolved))
        {
            return;
        }

        lock (SyncRoot)
        {
            IntPtr handle = LoadNativeLibrary(resolved!);
            if (handle == IntPtr.Zero)
            {
                throw new MpvException("Unable to load libmpv from: " + resolved);
            }

            LoadedLibraries.Add(handle);
            LoadedLibraryPaths.Add(Path.GetFullPath(resolved!));
        }
    }

    /// <summary>
    /// 從執行階段資料夾載入平台預設名稱的 libmpv 程式庫。
    /// </summary>
    /// <param name="runtimeDirectory">包含 libmpv 原生程式庫的執行階段資料夾。</param>
    public static void LoadFromRuntimeDirectory(string runtimeDirectory)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            throw new ArgumentException("Runtime directory is required.", nameof(runtimeDirectory));
        }

        Load(Path.Combine(runtimeDirectory, GetDefaultLibraryFileName()));
    }

    /// <summary>
    /// 取得目前平台預設的 libmpv 原生程式庫檔名。
    /// </summary>
    /// <returns>目前平台預設的 libmpv 檔名。</returns>
    public static string GetDefaultLibraryFileName()
    {
        return WindowsLibraryFileName;
    }

    /// <summary>
    /// 取得目前處理序對應的 .NET 執行階段識別碼。
    /// </summary>
    /// <returns>目前平台與處理器架構組成的執行階段識別碼。</returns>
    public static string GetDefaultRuntimeIdentifier()
    {
#if NET5_0_OR_GREATER || NETSTANDARD2_0
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.OSArchitecture != Architecture.X64 ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("目前只支援 Windows x64 runtime。");
        }
#else
        if (IntPtr.Size != 8)
        {
            throw new PlatformNotSupportedException("目前只支援 Windows x64 runtime。");
        }
#endif

        return "win-x64";
    }

    /// <summary>
    /// 解析使用者指定、環境變數或預設位置中的 libmpv 程式庫路徑。
    /// </summary>
    /// <param name="libraryPath">使用者指定的 libmpv 檔案或資料夾路徑。</param>
    /// <returns>可載入的 libmpv 檔案路徑；找不到時為 <see langword="null"/>。</returns>
    private static string? ResolveLibraryPath(string? libraryPath)
    {
        if (!string.IsNullOrWhiteSpace(libraryPath))
        {
            return ResolveFileOrDirectory(libraryPath!);
        }

        string? envPath = Environment.GetEnvironmentVariable("MPV_LIBRARY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return ResolveFileOrDirectory(envPath!);
        }

        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string libraryFileName = GetDefaultLibraryFileName();
        string rid = GetDefaultRuntimeIdentifier();
        string[] candidates =
        {
            Path.Combine(baseDirectory, libraryFileName),
            Path.Combine(baseDirectory, "runtimes", rid, "native", libraryFileName),
            Path.Combine(baseDirectory, "mpv", libraryFileName)
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 將檔案或資料夾路徑解析為 libmpv 檔案路徑。
    /// </summary>
    /// <param name="path">使用者提供的檔案或資料夾路徑。</param>
    /// <returns>解析後的 libmpv 檔案路徑。</returns>
    private static string ResolveFileOrDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            return Path.Combine(path, GetDefaultLibraryFileName());
        }

        return path;
    }

    /// <summary>
    /// 使用目前目標框架可用的原生載入器載入 libmpv。
    /// </summary>
    /// <param name="path">要載入的 libmpv 檔案路徑。</param>
    /// <returns>原生程式庫控制代碼。</returns>
    private static IntPtr LoadNativeLibrary(string path)
    {
#if NET5_0_OR_GREATER
        return NativeLibrary.Load(path);
#else
        return Kernel32.LoadLibrary(path);
#endif
    }

    /// <summary>
    /// 宣告 .NET Framework 使用的 Kernel32 原生載入 API。
    /// </summary>
    private static class Kernel32
    {
        /// <summary>
        /// 載入指定的 Windows 原生程式庫。
        /// </summary>
        /// <param name="lpFileName">要載入的原生程式庫檔案路徑。</param>
        /// <returns>載入後的原生程式庫控制代碼。</returns>
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string lpFileName);
    }

}
