using System;

using MediaEmbedKit.Mpv.Platforms;

namespace MediaEmbedKit.Mpv.Externals;

/// <summary>
/// 描述外部工具在特定平台上的執行階段來源。
/// </summary>
public sealed class ExternalToolRuntimeSource
{
    /// <summary>
    /// 初始化 <see cref="ExternalToolRuntimeSource"/> 類別的新執行個體。
    /// </summary>
    /// <param name="tool">
    /// 外部工具種類。
    /// </param>
    /// <param name="platform">
    /// 外部工具來源對應的平台。
    /// </param>
    /// <param name="name">
    /// 外部工具來源的顯示名稱。
    /// </param>
    /// <param name="uri">
    /// 外部工具來源的參考 URI。
    /// </param>
    /// <param name="assetName">
    /// 外部工具來源的發行資產名稱。
    /// </param>
    /// <param name="status">
    /// 此專案對該來源的支援狀態。
    /// </param>
    /// <param name="supportsSelfUpdate">
    /// 外部工具是否提供自我更新命令。
    /// </param>
    /// <param name="updateCommand">
    /// 外部工具自我更新命令範例。
    /// </param>
    /// <param name="note">
    /// 外部工具來源的補充說明。
    /// </param>
    internal ExternalToolRuntimeSource(
        ExternalToolKind tool,
        MpvNativeRuntimePlatform platform,
        string name,
        Uri uri,
        string assetName,
        MpvNativeRuntimeSupportStatus status,
        bool supportsSelfUpdate,
        string updateCommand,
        string note)
    {
        Tool = tool;
        Platform = platform;
        Name = name;
        Uri = uri;
        AssetName = assetName;
        Status = status;
        SupportsSelfUpdate = supportsSelfUpdate;
        UpdateCommand = updateCommand;
        Note = note;
    }

    /// <summary>
    /// 取得外部工具種類。
    /// </summary>
    /// <value>
    /// 外部工具種類。
    /// </value>
    public ExternalToolKind Tool { get; private set; }

    /// <summary>
    /// 取得外部工具來源對應的平台。
    /// </summary>
    /// <value>
    /// 外部工具來源平台。
    /// </value>
    public MpvNativeRuntimePlatform Platform { get; private set; }

    /// <summary>
    /// 取得外部工具來源的顯示名稱。
    /// </summary>
    /// <value>
    /// 外部工具來源名稱。
    /// </value>
    public string Name { get; private set; }

    /// <summary>
    /// 取得外部工具來源的參考 URI。
    /// </summary>
    /// <value>
    /// 外部工具來源 URI。
    /// </value>
    public Uri Uri { get; private set; }

    /// <summary>
    /// 取得外部工具來源的發行資產名稱。
    /// </summary>
    /// <value>
    /// 外部工具發行資產名稱。
    /// </value>
    public string AssetName { get; private set; }

    /// <summary>
    /// 取得此專案對該來源的支援狀態。
    /// </summary>
    /// <value>
    /// 外部工具來源支援狀態。
    /// </value>
    public MpvNativeRuntimeSupportStatus Status { get; private set; }

    /// <summary>
    /// 取得外部工具是否提供自我更新命令。
    /// </summary>
    /// <value>
    /// 工具可自行更新時為 <see langword="true"/>。
    /// </value>
    public bool SupportsSelfUpdate { get; private set; }

    /// <summary>
    /// 取得外部工具自我更新命令範例。
    /// </summary>
    /// <value>
    /// 外部工具更新命令文字。
    /// </value>
    public string UpdateCommand { get; private set; }

    /// <summary>
    /// 取得外部工具來源的補充說明。
    /// </summary>
    /// <value>
    /// 外部工具來源補充說明。
    /// </value>
    public string Note { get; private set; }
}
