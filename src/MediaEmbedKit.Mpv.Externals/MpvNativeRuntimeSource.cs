using System;

using MediaEmbedKit.Mpv.Platforms;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 描述特定平台可參考的原生 libmpv 來源。
/// </summary>
public sealed class MpvNativeRuntimeSource
{
    /// <summary>
    /// 初始化 <see cref="MpvNativeRuntimeSource"/> 類別的新執行個體。
    /// </summary>
    /// <param name="platform">
    /// 來源對應的原生平台。
    /// </param>
    /// <param name="name">
    /// 原生來源的顯示名稱。
    /// </param>
    /// <param name="uri">
    /// 原生來源的參考 URI。
    /// </param>
    /// <param name="kind">
    /// 原生來源的取得方式。
    /// </param>
    /// <param name="status">
    /// 此專案對該來源的支援狀態。
    /// </param>
    /// <param name="nativeLibraryName">
    /// 該平台預期的原生程式庫名稱。
    /// </param>
    /// <param name="note">
    /// 原生來源的補充說明。
    /// </param>
    internal MpvNativeRuntimeSource(
        MpvNativeRuntimePlatform platform,
        string name,
        Uri uri,
        MpvNativeRuntimeSourceKind kind,
        MpvNativeRuntimeSupportStatus status,
        string nativeLibraryName,
        string note)
    {
        Platform = platform;
        Name = name;
        Uri = uri;
        Kind = kind;
        Status = status;
        NativeLibraryName = nativeLibraryName;
        Note = note;
    }

    /// <summary>
    /// 取得來源對應的原生平台。
    /// </summary>
    /// <value>
    /// 原生平台。
    /// </value>
    public MpvNativeRuntimePlatform Platform { get; private set; }

    /// <summary>
    /// 取得原生來源的顯示名稱。
    /// </summary>
    /// <value>
    /// 原生來源名稱。
    /// </value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得原生來源的參考 URI。
    /// </summary>
    /// <value>
    /// 原生來源 URI。
    /// </value>
    public Uri Uri { get; private set; }

    /// <summary>
    /// 取得原生來源的取得方式。
    /// </summary>
    /// <value>
    /// 原生來源種類。
    /// </value>
    public MpvNativeRuntimeSourceKind Kind { get; private set; }

    /// <summary>
    /// 取得此專案對該來源的支援狀態。
    /// </summary>
    /// <value>
    /// 原生來源支援狀態。
    /// </value>
    public MpvNativeRuntimeSupportStatus Status { get; private set; }

    /// <summary>
    /// 取得該平台預期的原生程式庫名稱。
    /// </summary>
    /// <value>
    /// libmpv 原生程式庫名稱。
    /// </value>
    public string NativeLibraryName { get; private set; }

    /// <summary>
    /// 取得原生來源的補充說明。
    /// </summary>
    /// <value>
    /// 原生來源補充說明。
    /// </value>
    public string Note { get; private set; }
}
